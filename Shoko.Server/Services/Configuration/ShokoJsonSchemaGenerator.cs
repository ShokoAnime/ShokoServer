using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using NJsonSchema.NewtonsoftJson.Generation;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Plugin;

using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Shoko.Server.Services.Configuration;

/// <summary>
/// Responsible for generating JSON schema for Shoko configuration objects.
/// </summary>
/// <param name="newtonsoftJsonSerializerSettings">The Newtonsoft JSON serializer settings</param>
/// <param name="systemTextJsonSerializerOptions">The System.Text.Json serializer options</param>
public class ShokoJsonSchemaGenerator(JsonSerializerSettings newtonsoftJsonSerializerSettings, JsonSerializerOptions systemTextJsonSerializerOptions) : ISchemaProcessor, ISchemaNameGenerator
{
    private readonly JsonSerializerSettings _newtonsoftJsonSerializerSettings = newtonsoftJsonSerializerSettings;

    private readonly JsonSerializerOptions _systemTextJsonSerializerOptions = systemTextJsonSerializerOptions;

    private readonly object _lock = new();

    private Type? _currentType = null;

    /// <summary>
    ///   Which serialiser the type currently being walked is read and written
    ///   by. It used to be derived from the type itself, which only works for
    ///   configurations — an action is populated by
    ///   <see cref="JsonConvert.PopulateObject(string, object)"/> without
    ///   implementing <see cref="INewtonsoftJsonConfiguration"/>.
    /// </summary>
    private bool _isNewtonsoftJson = false;

    private Func<object, string?>? _enumValueConverter = null;

    private readonly Dictionary<string, UiClassBuilder> _schemaCache = [];

    private readonly Dictionary<JsonSchema, string> _schemaKeys = [];

    private readonly Regex _newlineCollapseRegex = new(@"(\r\n|\r|\n)+", RegexOptions.Compiled);

    /// <summary>
    ///   Generates the schema for a configuration type, on the serialiser the
    ///   configuration declared.
    /// </summary>
    /// <param name="type">The configuration type.</param>
    /// <returns>The schema and the typed builders that produced it.</returns>
    public WrappedJsonSchema GetSchemaForType(Type type)
        => GenerateSchema(type, type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration)), contractResolver: null);

    /// <summary>
    ///   Generates the schema for an executable action's invocation parameters.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     An action's parameters are its own settable, serialized properties —
    ///     <c>ActionService.PopulateParameters</c> populates the caller's
    ///     payload straight onto the action instance — so the walk is the same
    ///     one a configuration gets, and the produced
    ///     <see cref="Abstractions.UI.UiDefinition"/> is indistinguishable from
    ///     a configuration's. This entrypoint exists only to apply the two
    ///     rules that differ, in one place:
    ///   </para>
    ///   <list type="bullet">
    ///     <item>
    ///       Newtonsoft unconditionally, because that is what populates an
    ///       action; an action does not implement
    ///       <see cref="INewtonsoftJsonConfiguration"/> to say so.
    ///     </item>
    ///     <item>
    ///       The action's own metadata surface is not a parameter. See
    ///       <see cref="ActionMetadataContractResolver"/>.
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <param name="actionType">
    ///   The concrete action type, implementing
    ///   <see cref="IExecutableAction"/>.
    /// </param>
    /// <returns>The schema and the typed builders that produced it.</returns>
    public WrappedJsonSchema GetSchemaForActionParameters(Type actionType)
    {
        ArgumentNullException.ThrowIfNull(actionType);
        if (!actionType.IsAssignableTo(typeof(IExecutableAction)))
            throw new ArgumentException($"Type \"{actionType.FullName}\" does not implement {nameof(IExecutableAction)}.", nameof(actionType));

        var wrapped = GenerateSchema(actionType, isNewtonsoftJson: true, new ActionMetadataContractResolver());

        // There is nothing to save on an invocation form — the client renders an
        // invoke button of its own — so the root never offers the built-in save
        // action a configuration's root does.
        if (wrapped.UiBuilders.TryGetValue(wrapped.Schema, out var rootBuilder))
        {
            rootBuilder.HideSaveAction = true;
            rootBuilder.ShowSaveAction = false;
        }

        foreach (var objectSchema in EnumerateObjectSchemas(wrapped.Schema))
        {
            // A configuration document is read back leniently, so the generator
            // leaves objects open. An invocation payload is the opposite case:
            // `JsonConvert.PopulateObject` ignores a member it cannot map, so a
            // mistyped parameter name would silently do nothing and the action
            // would run on its defaults. Closing the object turns that into a
            // rejection.
            objectSchema.AllowAdditionalProperties = false;

            // Nothing is required of an invocation payload. The action instance
            // is already fully constructed with its own defaults before anything
            // is populated onto it, so an absent parameter means "leave it
            // alone" — and a caller supplying one parameter must not be forced
            // to supply the rest.
            foreach (var property in objectSchema.Properties.Values)
                property.IsRequired = false;
        }

        return wrapped;
    }

    /// <summary>
    ///   The root and every hoisted definition that is a plain object, skipping
    ///   enumerations and dictionaries.
    /// </summary>
    /// <remarks>
    ///   A dictionary carries its value schema in <c>additionalProperties</c>,
    ///   so closing it would throw the value type away and reject every entry.
    /// </remarks>
    private static IEnumerable<JsonSchema> EnumerateObjectSchemas(JsonSchema rootSchema)
        => rootSchema.Definitions.Values
            .Prepend(rootSchema)
            .Select(x => x.ActualSchema)
            .Where(x => !x.IsEnumeration && x.AdditionalPropertiesSchema is null && x.Properties.Count > 0)
            .Distinct();

    private WrappedJsonSchema GenerateSchema(Type type, bool isNewtonsoftJson, IContractResolver? contractResolver)
    {
        lock (_lock)
        {
            var generator = isNewtonsoftJson
                ? GetNewtonsoftSchemaForType(contractResolver)
                : GetSystemTextJsonSchemaForType();
            generator.Settings.SchemaProcessors.Add(this);

            // Handle built-in types NJsonSchema doesn't.
            generator.Settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Version), s =>
            {
                s.Type = JsonObjectType.String;
                s.Format = "version";
            }));

            _schemaCache.Clear();
            _schemaKeys.Clear();
            _enumValueConverter = null;
            _currentType = type;
            _isNewtonsoftJson = isNewtonsoftJson;
            var emitContext = new UiEmitContext
            {
                Convert = Convert,
                ConvertToken = value => Convert(value, isNewtonsoftJson),
            };
            var schema = generator.Generate(type);
            var uiBuilders = new Dictionary<JsonSchema, UiClassBuilder>();
            var wrappedSchema = new WrappedJsonSchema { Schema = schema, UiBuilders = uiBuilders, EmitContext = emitContext };
            var schemaDefinitions = schema.Definitions.Values.Where(s => !s.IsEnumeration).Prepend(schema).ToList();
            // Post-process the schema; add the UI definitions at the correct locations.
            foreach (var subSchema in schemaDefinitions)
            {
                subSchema.Description = string.IsNullOrEmpty(subSchema.Description) ? null : subSchema.Description.Replace(_newlineCollapseRegex, "\n");
                if (!_schemaKeys.TryGetValue(subSchema, out var schemaKey))
                    continue;

                if (!_schemaCache.TryGetValue(schemaKey, out var classBuilder))
                    continue;

                var isRootSchema = ReferenceEquals(schema, subSchema);
                if (classBuilder.IsPopulated)
                {
                    uiBuilders[subSchema] = classBuilder;
                    if (classBuilder.Actions.Count > 0)
                        wrappedSchema.HasCustomActions = true;
                    foreach (var reactiveAction in classBuilder.ReactiveActions)
                    {
                        switch (reactiveAction.ActionType)
                        {
                            case ConfigurationActionType.New when isRootSchema:
                                wrappedSchema.HasCustomNewFactory = true;
                                break;
                            case ConfigurationActionType.Validate when isRootSchema:
                                wrappedSchema.HasCustomValidation = true;
                                break;
                            case ConfigurationActionType.Save when isRootSchema:
                                wrappedSchema.HasCustomSave = true;
                                break;
                            case ConfigurationActionType.Load when isRootSchema:
                                wrappedSchema.HasCustomLoad = true;
                                break;
                            case ConfigurationActionType.LiveEdit:
                                wrappedSchema.HasLiveEdit = true;
                                break;
                        }
                    }
                    if (classBuilder.Label is { } classLabel)
                        subSchema.Title = classLabel;
                }
                foreach (var (propertyName, schemaValue) in subSchema.Properties)
                {
                    schemaValue.Description = string.IsNullOrEmpty(schemaValue.Description) ? null : schemaValue.Description.Replace(_newlineCollapseRegex, "\n");
                    var propertyKey = propertyName;
                    if (schemaValue.Item is not null)
                        propertyKey += "+List";
                    if (schemaValue.AdditionalPropertiesSchema is not null)
                        propertyKey += "+Dict";
                    if (classBuilder.GetProperty(propertyKey) is not { } propertyBuilder)
                        continue;

                    // Handle enum default values.
                    if (propertyBuilder.Element is UiEnumElementBuilder { Values: var enumValues } &&
                        schemaValue.Reference?.EnumerationNames?.Count is > 0 &&
                        schemaValue.Default is string defaultValue
                    )
                    {
                        var index = schemaValue.Reference.EnumerationNames.IndexOf(defaultValue);
                        schemaValue.Default = index >= 0 ? enumValues[index].Value : null;
                    }

                    if (propertyBuilder.Label is { } propertyLabel)
                        schemaValue.Title = propertyLabel;
                    if (propertyBuilder.ToExtensionData(emitContext) is { } uiDefinition)
                    {
                        schemaValue.ExtensionData ??= new Dictionary<string, object?>();
                        schemaValue.ExtensionData.Add(UiDefinition, uiDefinition);
                    }
                }
            }
            _schemaCache.Clear();
            _schemaKeys.Clear();
            _enumValueConverter = null;
            _currentType = null;
            _isNewtonsoftJson = false;
            return wrappedSchema;
        }
    }

    private JsonSchemaGenerator GetNewtonsoftSchemaForType(IContractResolver? contractResolver)
    {
        var serializerSettings = contractResolver is null
            ? _newtonsoftJsonSerializerSettings
            : new JsonSerializerSettings(_newtonsoftJsonSerializerSettings) { ContractResolver = contractResolver };
        var generator = new JsonSchemaGenerator(new NewtonsoftJsonSchemaGeneratorSettings
        {
            SerializerSettings = serializerSettings,
            SchemaType = SchemaType.JsonSchema,
            GenerateEnumMappingDescription = true,
            FlattenInheritanceHierarchy = true,
            AlwaysAllowAdditionalObjectProperties = true,
            AllowReferencesWithProperties = true,
            SchemaNameGenerator = this,
            XmlDocumentationFormatting = XmlDocsFormattingMode.Markdown,
        });
        return generator;
    }

    private JsonSchemaGenerator GetSystemTextJsonSchemaForType()
    {
        var generator = new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings
        {
            SerializerOptions = _systemTextJsonSerializerOptions,
            SchemaType = SchemaType.JsonSchema,
            GenerateEnumMappingDescription = true,
            FlattenInheritanceHierarchy = true,
            AlwaysAllowAdditionalObjectProperties = true,
            AllowReferencesWithProperties = true,
            SchemaNameGenerator = this,
            XmlDocumentationFormatting = XmlDocsFormattingMode.Markdown,
        });
        return generator;
    }

    #region Schema | Constants

    /// <summary>
    ///   The extension key the UI definition bag is stored under.
    /// </summary>
    /// <remarks>
    ///   The five constants in this region are the only string keys the
    ///   generator still needs to name: they are the ones
    ///   <see cref="ShokoJsonSchemaValidator{TConfig}"/> reads back off the
    ///   finished schema. Everything else moved into the typed builders in
    ///   <c>UiSchemaBuilders.cs</c>.
    /// </remarks>
    internal const string UiDefinition = "x-uiDefinition";

    internal const string ElementType = "elementType";

    internal const string ElementEnvironmentVariable = "envVar";

    internal const string ElementEnvironmentVariableOverridable = "envVarOverridable";

    internal const string ElementRequiresRestart = "requiresRestart";

    internal const string EnumDefinitions = "enumDefinitions";

    #endregion

    #region Schema | ISchemaProcessor

    void ISchemaProcessor.Process(SchemaProcessorContext context)
    {
        var schema = context.Schema.ActualSchema;
        var contextualType = context.ContextualType;
        _enumValueConverter ??= context.Settings.ReflectionService.GetEnumValueConverter(context.Settings);
        if (contextualType.Context is ContextualPropertyInfo info)
            ProcessProperty(schema, contextualType, info);

        // Add a reference to the class schema and generate the schema if it's not done yet.
        if (
            schema.Properties.Count > 0 &&
            (!contextualType.Type.FullName?.StartsWith("System.") ?? false) &&
            (!contextualType.Type.FullName?.StartsWith("Shoko.Abstractions.UI.Components.") ?? false)
        )
            ProcessClass(schema, contextualType);
    }

    private void ProcessProperty(JsonSchema schema, ContextualType contextualType, ContextualPropertyInfo info)
    {
        AssertNoNestedCollection(info);

        var classBuilder = GetOrAddClass(info.MemberInfo.ReflectedType!);
        var propertyName = GetPropertyKey(info);
        var propertyKey = propertyName;
        if (schema.Item is not null)
            propertyKey += "+List";
        if (schema.AdditionalPropertiesSchema is not null)
            propertyKey += "+Dict";
        var builder = classBuilder.GetOrAddProperty(propertyKey, () => new UiPropertyBuilder
        {
            Key = propertyKey,
            PropertyName = propertyName,
            MemberName = info.Name,
            SourceType = contextualType.Type,
            Property = info.PropertyInfo,
        });

        // The same property is visited once per schema node it produced, and
        // inherited properties are visited once per sub-class. Only the first
        // visit gets to describe it.
        if (builder.IsPopulated)
            return;
        builder.IsPopulated = true;

        if (info.GetAttribute<DisplayAttribute>(false) is { } displayAttribute)
        {
            builder.Label = !string.IsNullOrWhiteSpace(displayAttribute.Name)
                ? displayAttribute.Name
                : TypeReflectionExtensions.GetDisplayName(info.Name);
            if (!string.IsNullOrWhiteSpace(displayAttribute.GroupName))
                builder.Group = displayAttribute.GroupName;
        }
        else
        {
            builder.Label = TypeReflectionExtensions.GetDisplayName(info.Name);
        }

        builder.ElementSize = DisplayElementSize.Normal;
        if (info.GetAttribute<KeyAttribute>(false) is not null)
            builder.IsPrimaryKey = true;
        if (info.GetAttribute<DeniedValuesAttribute>(false) is { } deniedValuesAttribute)
            builder.DeniedValues = deniedValuesAttribute.Values.ToList();

        if (info.GetAttribute<SectionNameAttribute>(false) is { } sectionNameAttribute)
            builder.SectionName = sectionNameAttribute.Name;

        if (info.GetAttribute<VisibilityAttribute>(false) is { } visibilityAttribute)
        {
            builder.Visibility = new UiVisibilityBuilder
            {
                Default = visibilityAttribute.Visibility,
                Advanced = visibilityAttribute.Advanced,
                Toggle = visibilityAttribute.HasToggleCondition
                    ? new UiConditionBuilder
                    {
                        Path = visibilityAttribute.ToggleWhenMemberIsSet,
                        Value = visibilityAttribute.ToggleWhenSetTo,
                        Visibility = visibilityAttribute.ToggleVisibilityTo,
                        InverseCondition = visibilityAttribute.InverseToggleCondition,
                    }
                    : null,
                Disable = visibilityAttribute.HasDisableCondition
                    ? new UiConditionBuilder
                    {
                        Path = visibilityAttribute.DisableWhenMemberIsSet,
                        Value = visibilityAttribute.DisableWhenSetTo,
                        InverseCondition = visibilityAttribute.InverseDisableCondition,
                    }
                    : null,
            };
            builder.ElementSize = visibilityAttribute.Size;
        }

        builder.RequiresRestart = info.GetAttribute<RequiresRestartAttribute>(false) is not null;

        if (info.GetAttribute<EnvironmentVariableAttribute>(false) is { } environmentVariableAttribute && !string.IsNullOrWhiteSpace(environmentVariableAttribute.Name))
        {
            builder.EnvironmentVariable = environmentVariableAttribute.Name;
            builder.EnvironmentVariableOverridable = environmentVariableAttribute.AllowOverride;
        }

        if (info.GetAttribute<BadgeAttribute>(false) is { } badgeAttribute && !string.IsNullOrWhiteSpace(badgeAttribute.Name))
            builder.Badge = new UiBadgeBuilder { Name = badgeAttribute.Name, Theme = badgeAttribute.Theme };

        if (contextualType.Type.IsGenericType && contextualType.Type.GetGenericTypeDefinition() == typeof(SelectComponent<>))
        {
            var selectAttribute = info.GetAttribute<SelectAttribute>(false);
            builder.Element = new UiSelectElementBuilder
            {
                SelectType = selectAttribute?.SelectType ?? DisplaySelectType.Auto,
                MultipleItems = selectAttribute?.MultipleItems ?? false,
            };
        }
        else if (schema.Item is { } itemSchema)
        {
            var listAttribute = info.GetAttribute<ListAttribute>(false);
            var element = new UiListElementBuilder
            {
                ListType = listAttribute?.ListType ?? DisplayListType.Auto,
                Sortable = listAttribute?.Sortable ?? true,
                UniqueItems = listAttribute?.UniqueItems ?? false,
                HideAddAction = listAttribute?.HideAddAction ?? false,
                HideRemoveAction = listAttribute?.HideRemoveAction ?? false,
            };
            // Only set if the referenced schema is a class definition
            if (itemSchema.HasReference && _schemaKeys.TryGetValue(itemSchema.ActualSchema, out var referencedSchemaKey))
                element.ItemClass = _schemaCache[referencedSchemaKey];
            element.Item = classBuilder.GetProperty(propertyKey[..^5]);
            element.ItemElementType = ResolveInnerElementType(element.ItemClass, element.Item);
            builder.Element = element;

            var hasPrimaryKey = builder.IsPrimaryKey || element.ItemClass?.PrimaryKey is not null || element.Item is { IsPrimaryKey: true };
            switch (element.ListType)
            {
                case DisplayListType.ComplexDropdown:
                    if (element.ItemElementType is not DisplayElementType.SectionContainer)
                        throw new NotSupportedException("Dropdown lists are not supported for non-class list items.");
                    if (!hasPrimaryKey)
                        throw new NotSupportedException("Dropdown lists must have a primary key set.");
                    break;
                case DisplayListType.ComplexTab:
                    if (element.ItemElementType is not DisplayElementType.SectionContainer)
                        throw new NotSupportedException("Tab lists are not supported for non-class list items.");
                    if (!hasPrimaryKey)
                        throw new NotSupportedException("Tab lists must have a primary key set.");
                    break;
                case DisplayListType.ComplexInline:
                    if (element.ItemElementType is not DisplayElementType.SectionContainer)
                        throw new NotSupportedException("Inline lists are not supported for non-class list items.");
                    if (!hasPrimaryKey)
                        throw new NotSupportedException("Inline lists must have a primary key set.");
                    break;
                case DisplayListType.EnumCheckbox:
                    if (element.ItemElementType is not DisplayElementType.Enum)
                        throw new NotSupportedException("Checkbox lists are not supported for non-enum list items.");
                    break;
            }
        }
        else if (schema.AdditionalPropertiesSchema is { } recordSchema)
        {
            var (keyType, valueType) = GetTKeyAndTValue(info.PropertyType.Type);
            AssertKeyUsable(keyType);

            var recordAttribute = info.GetAttribute<RecordAttribute>(false);
            var element = new UiRecordElementBuilder
            {
                RecordType = recordAttribute?.RecordType ?? DisplayRecordType.Auto,
                Sortable = recordAttribute?.Sortable ?? true,
                HideAddAction = recordAttribute?.HideAddAction ?? false,
                HideRemoveAction = recordAttribute?.HideRemoveAction ?? false,
                KeyType = keyType,
                ValueType = valueType,
            };
            if (keyType.IsEnum)
            {
                element.KeyEnumValues = CollectEnumValues(keyType.ToContextualType()).Values;
                element.KeyEnumIsFlag = keyType.GetCustomAttribute<FlagsAttribute>() is not null;
            }
            // Only set if the referenced schema is a class definition
            if (recordSchema.HasReference && _schemaKeys.TryGetValue(recordSchema.ActualSchema, out var referencedSchemaKey))
                element.ValueClass = _schemaCache[referencedSchemaKey];
            element.Item = classBuilder.GetProperty(propertyKey[..^5]);
            element.ValueElementType = ResolveInnerElementType(element.ValueClass, element.Item);
            builder.Element = element;
        }
        else if (schema.IsEnumeration)
        {
            schema.Enumeration.Clear();
            schema.EnumerationNames.Clear();

            var (values, enumeration) = CollectEnumValues(contextualType);
            foreach (var (value, name) in enumeration)
            {
                schema.Enumeration.Add(value);
                schema.EnumerationNames.Add(name);
            }
            builder.Element = new UiEnumElementBuilder { Values = values, IsFlag = schema.IsFlagEnumerable };
        }
        else if (info.GetAttribute<CodeEditorAttribute>(false) is { } codeBlockAttribute)
        {
            builder.Element = new UiCodeEditorElementBuilder { Language = codeBlockAttribute.Language, AutoFormatOnLoad = codeBlockAttribute.AutoFormatOnLoad };
        }
        else if (info.GetAttribute<TextAreaAttribute>(false) is not null)
        {
            builder.Element = new UiTextAreaElementBuilder();
        }
        else if (info.GetAttribute<PasswordPropertyTextAttribute>(false) is not null)
        {
            builder.Element = new UiPasswordElementBuilder();
        }
    }

    /// <summary>
    ///   Works out what a collection's items should be rendered as: the item's
    ///   class definition first, then whatever the item's own property
    ///   definition resolved to. <c>null</c> means nothing overrode it.
    /// </summary>
    private static DisplayElementType? ResolveInnerElementType(UiClassBuilder? itemClass, UiPropertyBuilder? item)
    {
        DisplayElementType? elementType = itemClass is not null ? DisplayElementType.SectionContainer : null;
        if (item is not null && item.ElementType is not DisplayElementType.Auto)
            elementType = item.ElementType;
        return elementType;
    }

    private void ProcessClass(JsonSchema schema, ContextualType contextualType)
    {
        _schemaKeys.TryAdd(schema, contextualType.Type.FullName!);
        var classBuilder = GetOrAddClass(contextualType.Type);

        // Only generate the class schema once per type.
        if (classBuilder.IsPopulated)
            return;
        classBuilder.IsPopulated = true;

        if (contextualType.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrWhiteSpace(displayAttribute.Name))
            classBuilder.Label = displayAttribute.Name;

        classBuilder.HideSaveAction = contextualType.GetAttribute<HideDefaultSaveActionAttribute>(false) is not null;
        if (contextualType.GetAttribute<SectionAttribute>(false) is { } sectionTypeAttribute)
        {
            classBuilder.SectionType = sectionTypeAttribute.SectionType;
            if (!string.IsNullOrWhiteSpace(sectionTypeAttribute.DefaultSectionName))
                classBuilder.SectionName = sectionTypeAttribute.DefaultSectionName;
            classBuilder.AppendFloatingSectionsAtEnd = sectionTypeAttribute.AppendFloatingSectionsAtEnd;
            classBuilder.ShowSaveAction = sectionTypeAttribute.ShowSaveAction;
        }
        else
        {
            classBuilder.SectionType = DisplaySectionType.FieldSet;
        }

        classBuilder.PrimaryKey = classBuilder.Properties.FirstOrDefault(x => x.IsPrimaryKey)?.Key;

        var orderCount = 0;
        var knownGetters = new Dictionary<string, int>();
        var knownSetters = new Dictionary<string, int>();
        var knownMethods = new Dictionary<string, int>();
        var ignoredProperties = new HashSet<string>();
        var members = contextualType.Type.GetMembers()
            .OrderBy(x => x.GetCustomAttribute<DisplayAttribute>(false)?.GetOrder() ?? int.MaxValue)
            .ToList();
        foreach (var member in members)
        {
            var orderIndex = orderCount++;
            if (member.MemberType is MemberTypes.Method && (member.Name.StartsWith("get_") || member.Name.StartsWith("set_")))
            {
                if (member.Name.StartsWith("get_"))
                    knownGetters.TryAdd(member.Name[4..], orderIndex);
                else if (member.Name.StartsWith("set_"))
                    knownSetters.TryAdd(member.Name[4..], orderIndex);
                continue;
            }
            else if (member.CustomAttributes.Any(x => x.AttributeType == typeof(CustomActionAttribute)))
            {
                knownMethods.TryAdd(member.Name, orderIndex);
            }
            else if (member.MemberType is MemberTypes.Property or MemberTypes.Field)
            {
                knownGetters.TryAdd(member.Name, orderIndex);
                knownSetters.TryAdd(member.Name, orderIndex);
                if (IsIgnored(member))
                    ignoredProperties.Add(member.Name);
            }
        }
        classBuilder.Structure.AddRange(
            knownGetters
                .ExceptBy(ignoredProperties, a => a.Key)
                .IntersectBy(knownSetters.Keys, a => a.Key)
                .Select(x => KeyValuePair.Create(x.Key, (Order: Math.Min(x.Value, knownSetters[x.Key]), Type: UiMemberKind.Property)))
                .Union(
                    knownMethods
                        .Select(x => KeyValuePair.Create(x.Key, (Order: x.Value, Type: UiMemberKind.Method)))
                )
                .OrderBy(x => x.Value.Order)
                .Select(x => new UiMemberOrderEntry(x.Key, x.Value.Type))
        );

        var actions = contextualType.Methods
            .Where(x => x.GetAttribute<CustomActionAttribute>(false) is not null || x.GetAttribute<ConfigurationActionAttribute>(false) is not null)
            .ToArray();
        foreach (var methodInfo in actions)
        {
            var title = TypeReflectionExtensions.GetDisplayName(methodInfo, name => name.TrimEnd(" Action Handler").TrimEnd(" Handler").TrimEnd(" Action"));
            var description = TypeReflectionExtensions.GetDescription(methodInfo);
            if (methodInfo.GetAttribute<CustomActionAttribute>(false) is { } action)
            {
                classBuilder.Actions.Add(new UiActionBuilder
                {
                    ID = methodInfo.Name,
                    Title = title,
                    Description = description,
                    Theme = action.Theme,
                    Position = action.Position,
                    Size = action.Size,
                    Icon = string.IsNullOrWhiteSpace(action.Icon) ? null : action.Icon.Trim(),
                    MemberName = string.IsNullOrEmpty(action.AttachToMember) ? null : action.AttachToMember,
                    SectionName = string.IsNullOrEmpty(action.SectionName) ? null : action.SectionName,
                    Toggle = action.HasToggleCondition
                        ? new UiConditionBuilder { Path = action.ToggleWhenMemberIsSet, Value = action.ToggleWhenSetTo, InverseCondition = action.InverseToggleCondition }
                        : null,
                    Disable = action.HasDisableCondition
                        ? new UiConditionBuilder { Path = action.DisableWhenMemberIsSet, Value = action.DisableWhenSetTo, InverseCondition = action.InverseDisableCondition }
                        : null,
                    DisableIfNoChanges = action.DisableIfNoChanges,
                });
            }
            else if (methodInfo.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: var actionType, ReactiveEventType: var eventType })
            {
                classBuilder.ReactiveActions.Add(new UiReactiveActionBuilder
                {
                    ID = methodInfo.Name,
                    ActionType = actionType,
                    EventType = eventType,
                });
            }
        }
    }

    /// <summary>
    ///   Reads every member of an enumeration, collapsing members that share an
    ///   underlying value onto a single entry and recording the aliases.
    /// </summary>
    /// <param name="contextualType">The enumeration.</param>
    /// <returns>
    ///   The distinct values, and the value/name pairs the schema's own
    ///   enumeration should list — which includes both the serialised value and
    ///   the alias each member accepts on input.
    /// </returns>
    private (List<UiEnumValueBuilder> Values, List<(string Value, string Name)> Enumeration) CollectEnumValues(ContextualType contextualType)
    {
        var values = new List<UiEnumValueBuilder>();
        var enumeration = new List<(string Value, string Name)>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var enumName in Enum.GetNames(contextualType.Type))
        {
            var field = contextualType.GetField(enumName)!;
            var title = TypeReflectionExtensions.GetDisplayName(field);
            var description = TypeReflectionExtensions.GetDescription(field);

            var value = _enumValueConverter!(Enum.Parse(contextualType.Type, enumName))!;
            var newtonsoftValue = field.GetAttribute<EnumMemberAttribute>(false) is { } enumMemberAttribute && !string.IsNullOrEmpty(enumMemberAttribute.Value)
                ? enumMemberAttribute.Value : null;
            var systemTextJsonValue = field.GetAttribute<JsonStringEnumMemberNameAttribute>(false) is { } jsonStringEnumMemberNameAttribute && !string.IsNullOrEmpty(jsonStringEnumMemberNameAttribute.Name)
                ? jsonStringEnumMemberNameAttribute.Name : null;
            var overrideValue = IsNewtonsoftJson() ? newtonsoftValue : systemTextJsonValue;
            var aliasValue = overrideValue ?? enumName;
            if (aliasValue.Equals(value, StringComparison.Ordinal))
                aliasValue = string.Empty;

            if (known.Contains(value))
            {
                var entry = values.First(x => x.Value == value);
                entry.Alias = entry.Alias.Split(", ", StringSplitOptions.None | StringSplitOptions.RemoveEmptyEntries).Append(title).Except([entry.Title]).Distinct().Join(", ");
                entry.AliasValues = entry.AliasValues.Split(", ", StringSplitOptions.None | StringSplitOptions.RemoveEmptyEntries).Append(aliasValue).Except([entry.Value]).Distinct().Join(", ");
            }
            else
            {
                known.Add(value);
                values.Add(new UiEnumValueBuilder { Title = title, Description = description, Value = value, AliasValues = aliasValue });
                enumeration.Add((value, enumName));
            }
            if (!string.IsNullOrEmpty(aliasValue) && known.Add(aliasValue))
                enumeration.Add((aliasValue, enumName));
        }
        return (values, enumeration);
    }

    /// <summary>
    ///   Rejects a property whose collection holds another collection directly.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Every schema node a property produces is filed under the property's
    ///     name plus at most one <c>+List</c> and one <c>+Dict</c> marker, so
    ///     the two levels of a nested collection are indistinguishable. Today
    ///     that means a <c>List&lt;List&lt;T&gt;&gt;</c> or a
    ///     <c>Dictionary&lt;K, Dictionary&lt;K2, V&gt;&gt;</c> silently keeps
    ///     only the inner level's definition, a
    ///     <c>Dictionary&lt;K, List&lt;T&gt;&gt;</c> silently drops the inner
    ///     level's, and a <c>List&lt;Dictionary&lt;K, V&gt;&gt;</c> throws a
    ///     confusing error from the dictionary key resolver.
    ///   </para>
    ///   <para>
    ///     There is no sensible way to render a collection of collections
    ///     anyway, so they are all rejected up front with an error a plugin
    ///     author can act on. Wrapping the inner collection in a class both
    ///     fixes the ambiguity and gives the inner level somewhere to hang its
    ///     own label.
    ///   </para>
    /// </remarks>
    /// <param name="info">The property to check.</param>
    /// <exception cref="NotSupportedException">
    ///   The property is a collection of collections.
    /// </exception>
    private static void AssertNoNestedCollection(ContextualPropertyInfo info)
    {
        if (GetCollectionElementType(info.PropertyType.Type) is not { } elementType)
            return;
        if (GetCollectionElementType(elementType) is null)
            return;

        // A dictionary of collections is fine: the two levels get distinct keys
        // ("+Dict" and "+List"), so nothing collides and the schema is usable.
        // Only same-kind nesting shares a key, and only a dictionary inside a
        // list makes the key resolver read the outer type and throw.
        if (IsDictionary(info.PropertyType.Type).isDictionary && !IsDictionary(elementType).isDictionary)
            return;

        var declaringType = info.MemberInfo.DeclaringType is { } type ? GetFriendlyTypeName(type) : "?";
        throw new NotSupportedException(
            $"Configuration property \"{declaringType}.{info.Name}\" is a {GetFriendlyTypeName(info.PropertyType.Type)}. " +
            "A collection cannot hold another collection directly, because the UI has no way to render one and the schema " +
            "generator cannot tell the two levels apart. Wrap the inner collection in a class."
        );
    }

    /// <summary>
    ///   Returns what a collection type holds — the value type for a
    ///   dictionary, the element type otherwise — or <c>null</c> when the type
    ///   is not a collection. Strings are deliberately not collections here.
    /// </summary>
    private static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string))
            return null;
        if (IsDictionary(type).isDictionary)
            return GetTKeyAndTValue(type).ValueType;
        if (type.IsArray)
            return type.GetElementType();
        if (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];
        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    /// <summary>
    ///   Renders a type name the way it was written in source, so an error
    ///   message reads like the code that caused it.
    /// </summary>
    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsArray)
            return $"{GetFriendlyTypeName(type.GetElementType()!)}[]";
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var index = name.IndexOf('`', StringComparison.Ordinal);
        if (index >= 0)
            name = name[..index];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
    }

    /// <summary>
    ///   Returns the definition for a class, creating it — and the chain of
    ///   definitions for the classes it derives from — on first sight.
    /// </summary>
    /// <remarks>
    ///   The base chain is built eagerly so the order the generator happens to
    ///   visit a hierarchy in cannot matter. A definition that is never
    ///   populated is never emitted, so linking in an unused base costs
    ///   nothing but the empty container.
    /// </remarks>
    private UiClassBuilder GetOrAddClass(Type type)
    {
        var schemaKey = type.FullName!;
        if (_schemaCache.TryGetValue(schemaKey, out var builder))
            return builder;

        _schemaCache[schemaKey] = builder = new UiClassBuilder { Type = type };
        if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType) && baseType.FullName is not null)
            builder.BaseClass = GetOrAddClass(baseType);
        return builder;
    }

    private bool IsNewtonsoftJson() => _isNewtonsoftJson;

    private bool IsIgnored(MemberInfo memberInfo) =>
        IsNewtonsoftJson()
            ? memberInfo.CustomAttributes.Any(a => a.AttributeType == typeof(JsonIgnoreAttribute))
            : memberInfo.CustomAttributes.Any(a => a.AttributeType == typeof(System.Text.Json.Serialization.JsonIgnoreAttribute));

    private static (bool isDictionary, bool isReadonlyDictionary) IsDictionary(Type type)
    {
        var interfaces = type.GetInterfaces();
        var isExtendingReadonlyDictionary = (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)) || interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        var isExtendingWritableDictionary = (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)) || interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        return (isExtendingReadonlyDictionary || isExtendingWritableDictionary, isExtendingReadonlyDictionary);
    }

    private static (Type KeyType, Type ValueType) GetTKeyAndTValue(Type type)
    {
        Type[] arguments;
        var (isQualified, isReadonlyDictionary) = IsDictionary(type);
        if (!isQualified)
            throw new InvalidOperationException($"Type {type.Name} does not implement IReadOnlyDictionary<,> or IDictionary<,>.");

        if (!isReadonlyDictionary)
        {
            if (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                arguments = type.GetGenericArguments();
            else
                arguments = type.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)).GetGenericArguments();
        }
        else
        {
            if (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                arguments = type.GetGenericArguments();
            else
                arguments = type.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)).GetGenericArguments();
        }

        return (arguments[0], arguments[1]);
    }

    private void AssertKeyUsable(Type keyType)
    {
        if (keyType == typeof(string) || keyType.GetTypeInfo().IsEnum)
            return;

        if (keyType.GetCustomAttribute<SerializableAttribute>() is not null)
            return;

        if (!IsNewtonsoftJson() && keyType.GetCustomAttribute<JsonSerializableAttribute>() is not null)
            return;

        var interfaces = keyType.GetInterfaces();
        if (interfaces.Any(i => i == typeof(ISerializable)))
            return;

        throw new ArgumentException($"Type \"{keyType.FullName!}\" is not serializable to text and therefore cannot be used as a key in a dictionary inside a configuration.", nameof(keyType));
    }

    private static string GetPropertyKey(ContextualPropertyInfo info)
    {
        if (info.GetAttribute<JsonPropertyAttribute>(false) is { } jsonPropertyAttribute)
            return jsonPropertyAttribute.PropertyName ?? info.Name;
        if (info.GetAttribute<JsonPropertyNameAttribute>(false) is { } jsonPropertyNameAttribute)
            return jsonPropertyNameAttribute.Name ?? info.Name;
        return info.Name;
    }

    // For the values that needs to be converted by the right library in the right way
    private JToken? Convert(object? value, bool isNewtonsoftJson)
        => value is null ? null : isNewtonsoftJson
            ? JToken.Parse(JsonConvert.SerializeObject(value, Formatting.None, _newtonsoftJsonSerializerSettings))
            : JToken.Parse(JsonSerializer.Serialize(value, _systemTextJsonSerializerOptions));

    // For the values that needs to be converted by the right library in the right way
    private object? Convert(object? value)
        => JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value, Formatting.None, _newtonsoftJsonSerializerSettings))!;

    #endregion

    #region Schema | ISchemaNameGenerator

    string ISchemaNameGenerator.Generate(Type type)
        => GetDisplayName(type.ToContextualType());

    private static readonly HashSet<string> _configurationSuffixSet = ["Setting", "Conf", "Config", "Configuration"];

    public static string GetDisplayName(ContextualType contextualType)
    {
        if (contextualType.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrEmpty(displayAttribute.Name))
            return displayAttribute.Name;

        var name = TypeReflectionExtensions.GetDisplayName(contextualType);
        var offset = 0;
        retryNewNameLabel:;
        foreach (var suffix in _configurationSuffixSet)
        {
            if (name == suffix)
            {
                // I don't want to deal with generic types rn, so bail.
                if (contextualType.Type.IsGenericType)
                    break;

                name = TypeReflectionExtensions.GetDisplayName(contextualType.Type.FullName!.Split('.').Reverse().Skip(++offset).FirstOrDefault() ?? string.Empty);
                if (string.IsNullOrEmpty(name))
                    return TypeReflectionExtensions.GetDisplayName(contextualType);
                goto retryNewNameLabel;
            }

            var endsWith = $" {suffix}";
            if (name.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
                name = name[..^endsWith.Length];
            if (name.EndsWith($"{endsWith}s", StringComparison.OrdinalIgnoreCase))
                name = name[..^endsWith.Length];
        }

        return name.Trim();
    }

    #endregion
}
