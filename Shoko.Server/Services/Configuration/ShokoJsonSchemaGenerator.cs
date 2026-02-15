using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using NJsonSchema.NewtonsoftJson.Generation;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Components;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Plugin;

#nullable enable
namespace Shoko.Server.Services.Configuration;

/// <summary>
/// Responsible for generating JSON schema for Shoko configuration objects.
/// </summary>
/// <param name="newtonsoftJsonSerializerSettings">The Newtonsoft JSON serializer settings</param>
/// <param name="systemTextJsonSerializerOptions">The System.Text.Json serializer options</param>
public class ShokoJsonSchemaGenerator(JsonSerializerSettings newtonsoftJsonSerializerSettings, System.Text.Json.JsonSerializerOptions systemTextJsonSerializerOptions) : ISchemaProcessor, ISchemaNameGenerator
{
    private readonly JsonSerializerSettings _newtonsoftJsonSerializerSettings = newtonsoftJsonSerializerSettings;

    private readonly System.Text.Json.JsonSerializerOptions _systemTextJsonSerializerOptions = systemTextJsonSerializerOptions;

    private readonly object _lock = new();

    private Type? _currentType = null;

    private readonly Dictionary<string, (Dictionary<string, object?> ClassUIDefinition, Dictionary<string, Dictionary<string, object?>> PropertyUIDefinitions)> _schemaCache = [];

    private readonly Dictionary<JsonSchema, string> _schemaKeys = [];

    private readonly Regex _newlineCollapseRegex = new(@"(\r\n|\r|\n)+", RegexOptions.Compiled);

    public WrappedJsonSchema GetSchemaForType(Type type)
    {
        lock (_lock)
        {
            var isNewtonsoftJson = type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration));
            var generator = isNewtonsoftJson
                ? GetNewtonsoftSchemaForType()
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
            _currentType = type;
            var schema = generator.Generate(type);
            var wrappedSchema = new WrappedJsonSchema { Schema = schema };
            var schemaDefinitions = schema.Definitions.Values.Where(s => !s.IsEnumeration).Prepend(schema).ToList();
            // Post-process the schema; add the UI definitions at the correct locations.
            foreach (var subSchema in schemaDefinitions)
            {
                subSchema.Description = string.IsNullOrEmpty(subSchema.Description) ? null : subSchema.Description.Replace(_newlineCollapseRegex, "\n");
                if (!_schemaKeys.TryGetValue(subSchema, out var schemaKey))
                    continue;

                if (!_schemaCache.TryGetValue(schemaKey, out var schemaTuple))
                    continue;
                var (classDefinition, propertyDict) = schemaTuple;
                if (classDefinition.Count > 0)
                {
                    if (classDefinition.TryGetValue(ElementLabel, out var classLabel))
                    {
                        classDefinition.Remove(ElementLabel);
                        subSchema.Title = (string)classLabel!;
                    }
                    if (classDefinition[ElementActions] is Dictionary<string, Dictionary<string, object?>> customActionDict)
                    {
                        if (customActionDict.Count > 0)
                            wrappedSchema.HasCustomActions = true;
                    }
                    if (classDefinition[ElementReactiveActions] is List<Dictionary<string, object?>> c2)
                    {
                        foreach (var reactiveAction in c2)
                        {
                            switch ((string)reactiveAction[ActionType]!)
                            {
                                case "new" when ReferenceEquals(schema, subSchema):
                                    wrappedSchema.HasCustomNewFactory = true;
                                    break;
                                case "validate" when ReferenceEquals(schema, subSchema):
                                    wrappedSchema.HasCustomValidation = true;
                                    break;
                                case "save" when ReferenceEquals(schema, subSchema):
                                    wrappedSchema.HasCustomSave = true;
                                    break;
                                case "load" when ReferenceEquals(schema, subSchema):
                                    wrappedSchema.HasCustomLoad = true;
                                    break;
                                case "live-edit":
                                    wrappedSchema.HasLiveEdit = true;
                                    break;
                            }
                        }
                    }
                    if (ReferenceEquals(subSchema, schema))
                    {
                        classDefinition[ElementShowSaveAction] = classDefinition[ElementShowSaveAction] is true || classDefinition[ElementHideSaveAction] is not true;
                    }
                    classDefinition.Remove(ElementReactiveActions);
                    classDefinition.Remove(ElementHideSaveAction);
                    subSchema.ExtensionData ??= new Dictionary<string, object?>();
                    subSchema.ExtensionData.Add(UiDefinition, classDefinition);
                }
                foreach (var tuple in subSchema.Properties)
                {
                    var (propertyKey, schemaValue) = tuple;
                    schemaValue.Description = string.IsNullOrEmpty(schemaValue.Description) ? null : schemaValue.Description.Replace(_newlineCollapseRegex, "\n");
                    if (schemaValue.Item is not null)
                        propertyKey += "+List";
                    if (schemaValue.AdditionalPropertiesSchema is not null)
                        propertyKey += "+Dict";
                    if (!propertyDict.TryGetValue(propertyKey, out var propertyDefinition))
                        continue;

                    if (propertyDefinition.TryGetValue(ElementLabel, out var propertyLabel
                    ))
                    {
                        propertyDefinition.Remove(ElementLabel);
                        schemaValue.Title = (string)propertyLabel!;
                    }
                    schemaValue.ExtensionData ??= new Dictionary<string, object?>();
                    schemaValue.ExtensionData.Add(UiDefinition, propertyDefinition);
                }
            }
            _schemaCache.Clear();
            _schemaKeys.Clear();
            _currentType = null;
            return wrappedSchema;
        }
    }

    private JsonSchemaGenerator GetNewtonsoftSchemaForType()
    {
        var generator = new JsonSchemaGenerator(new NewtonsoftJsonSchemaGeneratorSettings
        {
            SerializerSettings = _newtonsoftJsonSerializerSettings,
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

    internal const string UiDefinition = "x-uiDefinition";

    private const string ElementType = "elementType";

    private const string ElementSize = "elementSize";

    private const string ElementStructure = "structure";

    private const string ElementLabel = "label";

    private const string ElementGroup = "group";

    private const string ElementHideSaveAction = "hideSaveAction";

    private const string ElementShowSaveAction = "showSaveAction";

    private const string ElementActions = "actions";

    private const string ElementReactiveActions = "reactiveActions";

    private const string ElementVisibility = "visibility";

    private const string ElementBadge = "badge";

    private const string ElementPrimaryKey = "primaryKey";

    private const string ElementDeniedValues = "deniedValues";

    internal const string ElementEnvironmentVariable = "envVar";

    internal const string ElementEnvironmentVariableOverridable = "envVarOverridable";

    internal const string ElementRequiresRestart = "requiresRestart";

    private const string SectionType = "sectionType";

    private const string ActionIdentity = "id";

    private const string ActionName = "title";

    private const string ActionDescription = "description";

    private const string ActionTheme = "theme";

    private const string ActionPosition = "position";

    private const string ActionType = "type";

    private const string ActionEventType = "eventType";

    private const string ActionSize = "size";

    private const string ActionIcon = "icon";

    private const string ActionVisibilityToggle = "toggle";

    private const string ActionDisableToggle = "disableToggle";

    private const string ActionDisableIfNoChanges = "disableIfNoChanges";

    private const string ActionMemberName = "memberName";

    private const string SectionName = "sectionName";

    private const string SectionAppendFloatingAtEnd = "sectionAppendFloatingAtEnd";

    private const string ListType = "listType";

    private const string ListSortable = "listSortable";

    private const string ListUniqueItems = "listUniqueItems";

    private const string ListHideAddAction = "listHideAddAction";

    private const string ListHideRemoveAction = "listHideRemoveAction";

    private const string ListElementType = "listElementType";

    private const string SelectType = "selectType";

    private const string SelectMultipleItems = "selectMultipleItems";

    private const string SelectElementType = "selectElementType";

    private const string RecordElementType = "recordElementType";

    private const string RecordType = "recordType";

    private const string RecordSortable = "recordSortable";

    private const string RecordHideAddAction = "recordHideAddAction";

    private const string RecordHideRemoveAction = "recordHideRemoveAction";

    private const string CodeBlockLanguage = "codeLanguage";

    private const string CodeBlockAutoFormatOnLoad = "codeAutoFormatOnLoad";

    private const string EnumDefinitions = "enumDefinitions";

    private const string EnumIsFlag = "enumIsFlag";

    #endregion

    #region Schema | ISchemaProcessor

    void ISchemaProcessor.Process(SchemaProcessorContext context)
    {
        var schema = context.Schema.ActualSchema;
        var contextualType = context.ContextualType;
        if (contextualType.Context is ContextualPropertyInfo info)
        {
            var uiDict = new Dictionary<string, object?>();
            var schemaKey = info.MemberInfo.ReflectedType!.FullName!;
            _schemaCache.TryAdd(schemaKey, ([], []));
            var propertyKey = GetPropertyKey(info);
            if (schema.Item is not null)
                propertyKey += "+List";
            if (schema.AdditionalPropertiesSchema is not null)
                propertyKey += "+Dict";
            if (!_schemaCache[schemaKey].PropertyUIDefinitions.TryAdd(propertyKey, uiDict))
                uiDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey];

            if (info.GetAttribute<DisplayAttribute>(false) is { } displayAttribute)
            {
                if (!string.IsNullOrWhiteSpace(displayAttribute.Name))
                    uiDict.TryAdd(ElementLabel, displayAttribute.Name);
                else
                    uiDict.TryAdd(ElementLabel, TypeReflectionExtensions.GetDisplayName(info.Name));
                if (!string.IsNullOrWhiteSpace(displayAttribute.GroupName))
                    uiDict.Add(ElementGroup, displayAttribute.GroupName);
            }
            else
            {
                uiDict.TryAdd(ElementLabel, TypeReflectionExtensions.GetDisplayName(info.Name));
            }

            uiDict.TryAdd(ElementType, Convert(DisplayElementType.Auto));
            uiDict.TryAdd(ElementSize, Convert(DisplayElementSize.Normal));
            if (info.GetAttribute<KeyAttribute>(false) is { })
                uiDict.Add(ElementPrimaryKey, true);
            if (info.GetAttribute<DeniedValuesAttribute>(false) is { } deniedValuesAttribute)
                uiDict.Add(ElementDeniedValues, deniedValuesAttribute.Values.Select(Convert).ToList());

            if (info.GetAttribute<SectionNameAttribute>(false) is { } sectionNameAttribute)
                uiDict.Add(SectionName, sectionNameAttribute.Name);

            if (info.GetAttribute<VisibilityAttribute>(false) is { } visibilityAttribute)
            {
                var visibilityDict = new Dictionary<string, object?>()
                {
                    { "default", Convert(visibilityAttribute.Visibility) },
                    { "advanced", visibilityAttribute.Advanced },
                };
                if (visibilityAttribute.HasToggleCondition)
                {
                    var toggleDict = new Dictionary<string, object?>()
                    {
                        { "path", visibilityAttribute.ToggleWhenMemberIsSet },
                        { "value", Convert(visibilityAttribute.ToggleWhenSetTo, _currentType!) },
                        { "visibility", Convert(visibilityAttribute.ToggleVisibilityTo) },
                    };
                    visibilityDict.Add(ActionVisibilityToggle, toggleDict);
                }
                else
                {
                    visibilityDict.Add(ActionVisibilityToggle, null);
                }
                if (visibilityAttribute.HasDisableCondition)
                {
                    var disableDict = new Dictionary<string, object?>()
                    {
                        { "path", visibilityAttribute.DisableWhenMemberIsSet },
                        { "value", Convert(visibilityAttribute.DisableWhenSetTo, _currentType!) },
                        { "inverseCondition", visibilityAttribute.InverseDisableCondition },
                    };
                    visibilityDict.Add(ActionDisableToggle, disableDict);
                }
                else
                {
                    visibilityDict.Add(ActionDisableToggle, null);
                }
                uiDict.Add(ElementVisibility, visibilityDict);
                uiDict[ElementSize] = Convert(visibilityAttribute.Size);
            }

            if (info.GetAttribute<RequiresRestartAttribute>(false) is { })
            {
                uiDict.Add(ElementRequiresRestart, true);
            }
            else
            {
                uiDict.Add(ElementRequiresRestart, false);
            }

            if (info.GetAttribute<EnvironmentVariableAttribute>(false) is { } environmentVariableAttribute && !string.IsNullOrWhiteSpace(environmentVariableAttribute.Name))
            {
                uiDict.Add(ElementEnvironmentVariable, environmentVariableAttribute.Name);
                uiDict.Add(ElementEnvironmentVariableOverridable, environmentVariableAttribute.AllowOverride);
            }

            if (info.GetAttribute<BadgeAttribute>(false) is { } badgeAttribute && !string.IsNullOrWhiteSpace(badgeAttribute.Name))
            {
                var badgeDict = new Dictionary<string, object?>
                {
                    { "name", badgeAttribute.Name },
                    { "theme", Convert(badgeAttribute.Theme) },
                };
                uiDict.Add(ElementBadge, badgeDict);
            }

            if (contextualType.Type.IsGenericType && contextualType.Type.GetGenericTypeDefinition() == typeof(SelectComponent<>))
            {
                uiDict[ElementType] = Convert(DisplayElementType.Select);
                uiDict.Add(SelectElementType, Convert(DisplayElementSize.Normal));
                if (info.GetAttribute<SelectAttribute>(false) is { } selectAttribute)
                {
                    uiDict.Add(SelectType, Convert(selectAttribute.SelectType));
                    uiDict.Add(SelectMultipleItems, selectAttribute.MultipleItems);
                }
                else
                {
                    uiDict.Add(SelectType, Convert(DisplaySelectType.Auto));
                    uiDict.Add(SelectMultipleItems, false);
                }
            }
            else if (schema.Item is { } itemSchema)
            {
                uiDict[ElementType] = Convert(DisplayElementType.List);
                uiDict.Add(ListElementType, Convert(DisplayElementSize.Normal));
                if (info.GetAttribute<ListAttribute>(false) is { } listAttribute)
                {
                    uiDict.Add(ListType, Convert(listAttribute.ListType));
                    uiDict.Add(ListSortable, listAttribute.Sortable);
                    uiDict.Add(ListUniqueItems, listAttribute.UniqueItems);
                    uiDict.Add(ListHideAddAction, listAttribute.HideAddAction);
                    uiDict.Add(ListHideRemoveAction, listAttribute.HideRemoveAction);
                }
                else
                {
                    uiDict.Add(ListType, Convert(DisplayListType.Auto));
                    uiDict.Add(ListSortable, true);
                    uiDict.Add(ListUniqueItems, false);
                    uiDict.Add(ListHideAddAction, false);
                    uiDict.Add(ListHideRemoveAction, false);
                }

                // Only set if the referenced schema is a class definition
                if (itemSchema.HasReference && _schemaKeys.TryGetValue(itemSchema.ActualSchema, out var referencedSchemaKey))
                {
                    var referencedDict = _schemaCache[referencedSchemaKey].ClassUIDefinition;
                    foreach (var (key, value) in referencedDict)
                    {
                        if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                            uiDict[ListElementType] = value;
                        else if (key is SectionType or ElementPrimaryKey or ElementDeniedValues or ElementGroup)
                            uiDict.TryAdd(key, value);
                    }
                }

                var innerDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey[..^5]];
                foreach (var (key, value) in innerDict)
                {
                    if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                        uiDict[ListElementType] = value;
                    else if (key is not ElementType)
                        uiDict.TryAdd(key, value);
                }

                if (Equals(uiDict[ListType], Convert(DisplayListType.ComplexDropdown)))
                {
                    if (!Equals(uiDict[ListElementType], Convert(DisplayElementType.SectionContainer)))
                        throw new NotSupportedException("Dropdown lists are not supported for non-class list items.");
                    if (!uiDict.ContainsKey(ElementPrimaryKey))
                        throw new NotSupportedException("Dropdown lists must have a primary key set.");
                }
                if (Equals(uiDict[ListType], Convert(DisplayListType.ComplexTab)))
                {
                    if (!Equals(uiDict[ListElementType], Convert(DisplayElementType.SectionContainer)))
                        throw new NotSupportedException("Tab lists are not supported for non-class list items.");
                    if (!uiDict.ContainsKey(ElementPrimaryKey))
                        throw new NotSupportedException("Tab lists must have a primary key set.");
                }
                if (Equals(uiDict[ListType], Convert(DisplayListType.ComplexInline)))
                {
                    if (!Equals(uiDict[ListElementType], Convert(DisplayElementType.SectionContainer)))
                        throw new NotSupportedException("Inline lists are not supported for non-class list items.");
                    if (!uiDict.ContainsKey(ElementPrimaryKey))
                        throw new NotSupportedException("Inline lists must have a primary key set.");
                }
                else if (Equals(uiDict[ListType], Convert(DisplayListType.EnumCheckbox)))
                {
                    if (!Equals(uiDict[ListElementType], Convert(DisplayElementType.Enum)))
                        throw new NotSupportedException("Checkbox lists are not supported for non-enum list items.");
                }
            }
            else if (schema.AdditionalPropertiesSchema is { } recordSchema)
            {
                var (keyType, valueType) = GetTKeyAndTValue(info.PropertyType.Type);
                AssertKeyUsable(keyType);

                uiDict[ElementType] = Convert(DisplayElementType.Record);
                uiDict.Add(RecordElementType, Convert(DisplayElementSize.Normal));
                if (info.GetAttribute<RecordAttribute>(false) is { } recordAttribute)
                {
                    uiDict.Add(RecordType, Convert(recordAttribute.RecordType));
                    uiDict.Add(RecordSortable, recordAttribute.Sortable);
                    uiDict.Add(RecordHideAddAction, recordAttribute.HideAddAction);
                    uiDict.Add(RecordHideRemoveAction, recordAttribute.HideRemoveAction);
                }
                else
                {
                    uiDict.Add(RecordType, Convert(DisplayRecordType.Auto));
                    uiDict.Add(RecordSortable, true);
                    uiDict.Add(RecordHideAddAction, false);
                    uiDict.Add(RecordHideRemoveAction, false);
                }

                // Only set if the referenced schema is a class definition
                if (recordSchema.HasReference && _schemaKeys.TryGetValue(recordSchema.ActualSchema, out var referencedSchemaKey))
                {
                    var referencedDict = _schemaCache[referencedSchemaKey].ClassUIDefinition;
                    foreach (var (key, value) in referencedDict)
                    {
                        if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                            uiDict[RecordElementType] = value;
                        else if (key is SectionType or ElementPrimaryKey or ElementDeniedValues or ElementGroup)
                            uiDict.TryAdd(key, value);
                    }
                }

                var innerDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey[..^5]];
                foreach (var (key, value) in innerDict)
                {
                    if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                        uiDict[RecordElementType] = value;
                    else if (key is not ElementType and not ElementSize)
                        uiDict.TryAdd(key, value);
                }
            }
            else if (schema.IsEnumeration)
            {
                schema.Enumeration.Clear();
                schema.EnumerationNames.Clear();

                var enumList = new List<Dictionary<string, object?>>();
                var enumValueConverter = context.Settings.ReflectionService.GetEnumValueConverter(context.Settings);
                foreach (var enumName in Enum.GetNames(contextualType.Type))
                {
                    string? value = null;
                    var field = contextualType.GetField(enumName)!;
                    var title = TypeReflectionExtensions.GetDisplayName(field);
                    var description = TypeReflectionExtensions.GetDescription(field);
                    if (field.GetAttribute<EnumMemberAttribute>(false) is { } enumMemberAttribute && !string.IsNullOrEmpty(enumMemberAttribute.Value))
                        value = enumMemberAttribute.Value;
                    else
                        value = enumValueConverter(Enum.Parse(contextualType.Type, enumName));

                    schema.Enumeration.Add(value);
                    schema.EnumerationNames.Add(enumName);
                    enumList.Add(new()
                    {
                        { "title", title },
                        { "description", description },
                        { "value", value },
                    });
                }
                uiDict[ElementType] = Convert(DisplayElementType.Enum);
                uiDict.Add(EnumDefinitions, enumList);
                uiDict.Add(EnumIsFlag, schema.IsFlagEnumerable);
            }
            else if (info.GetAttribute<CodeEditorAttribute>(false) is { } codeBlockAttribute)
            {
                uiDict[ElementType] = Convert(DisplayElementType.CodeBlock);
                uiDict[CodeBlockLanguage] = Convert(codeBlockAttribute.Language);
                uiDict[CodeBlockAutoFormatOnLoad] = codeBlockAttribute.AutoFormatOnLoad;
            }
            else if (info.GetAttribute<TextAreaAttribute>(false) is not null)
            {
                uiDict[ElementType] = Convert(DisplayElementType.TextArea);
            }
            else if (info.GetAttribute<PasswordPropertyTextAttribute>(false) is not null)
            {
                uiDict[ElementType] = Convert(DisplayElementType.Password);
            }
        }

        // Add a reference to the class schema and generate the schema if it's not done yet.
        if (
            schema.Properties.Count > 0 &&
            (!contextualType.Type.FullName?.StartsWith("System.") ?? false) &&
            (!contextualType.Type.FullName?.StartsWith("Shoko.Abstractions.Config.Components.") ?? false)
        )
        {
            var schemaKey = contextualType.Type.FullName!;
            _schemaKeys.TryAdd(schema, schemaKey);
            _schemaCache.TryAdd(schemaKey, ([], []));

            // Only generate the class schema once per type.
            if (_schemaCache[schemaKey].ClassUIDefinition is { Count: 0 } uiDict)
            {
                uiDict.Add(ElementType, Convert(DisplayElementType.SectionContainer));
                if (contextualType.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrWhiteSpace(displayAttribute.Name))
                    uiDict.Add(ElementLabel, displayAttribute.Name);

                var hideSaveAction = contextualType.GetAttribute<HideDefaultSaveActionAttribute>(false) is not null;
                var showSaveAction = false;
                if (contextualType.GetAttribute<SectionAttribute>(false) is { } sectionTypeAttribute)
                {
                    uiDict.Add(SectionType, Convert(sectionTypeAttribute.SectionType));
                    if (!string.IsNullOrWhiteSpace(sectionTypeAttribute.DefaultSectionName))
                        uiDict.Add(SectionName, sectionTypeAttribute.DefaultSectionName);
                    uiDict.Add(SectionAppendFloatingAtEnd, sectionTypeAttribute.AppendFloatingSectionsAtEnd);
                    showSaveAction = sectionTypeAttribute.ShowSaveAction;
                }
                else
                {
                    uiDict.Add(SectionType, Convert(DisplaySectionType.FieldSet));
                }

                var propertyDefinitions = _schemaCache[schemaKey].PropertyUIDefinitions;
                var primaryKey = propertyDefinitions.FirstOrDefault(x => x.Value.ContainsKey(ElementPrimaryKey) && x.Value[ElementPrimaryKey] is true).Key;
                if (!string.IsNullOrEmpty(primaryKey))
                    uiDict.Add(ElementPrimaryKey, primaryKey);

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
                    else if (member.MemberType is MemberTypes.Property)
                    {
                        knownGetters.TryAdd(member.Name, orderIndex);
                        knownSetters.TryAdd(member.Name, orderIndex);
                        if (IsIgnored(member))
                            ignoredProperties.Add(member.Name);
                    }
                    else if (member.MemberType is MemberTypes.Field)
                    {
                        knownGetters.TryAdd(member.Name, orderIndex);
                        knownSetters.TryAdd(member.Name, orderIndex);
                        if (IsIgnored(member))
                            ignoredProperties.Add(member.Name);
                    }
                }
                var orderDict = knownGetters
                    .ExceptBy(ignoredProperties, a => a.Key)
                    .IntersectBy(knownSetters.Keys, a => a.Key)
                    .Select(x => KeyValuePair.Create(x.Key, (Order: Math.Min(x.Value, knownSetters[x.Key]), Type: "property")))
                    .Union(
                        knownMethods
                            .Select(x => KeyValuePair.Create(x.Key, (Order: x.Value, Type: "method")))
                    )
                    .OrderBy(x => x.Value.Order)
                    .Select((kv) => KeyValuePair.Create(kv.Key, kv.Value.Type))
                    .ToDictionary();
                uiDict.Add(ElementStructure, orderDict);

                var actions = contextualType.Methods
                    .Where(x => x.GetAttribute<CustomActionAttribute>(false) is not null || x.GetAttribute<ConfigurationActionAttribute>(false) is not null)
                    .ToArray();
                var actionList = new List<KeyValuePair<string, Dictionary<string, object?>>>();
                var reactiveActionList = new List<Dictionary<string, object?>>();
                foreach (var methodInfo in actions)
                {
                    var title = TypeReflectionExtensions.GetDisplayName(methodInfo, name => name.TrimEnd(" Action Handler").TrimEnd(" Handler").TrimEnd(" Action"));
                    var description = TypeReflectionExtensions.GetDescription(methodInfo);
                    if (methodInfo.GetAttribute<CustomActionAttribute>(false) is { } action)
                    {
                        var actionDict = new Dictionary<string, object?>
                        {
                            { ActionName, title },
                            { ActionDescription, description },
                            { ActionTheme, Convert(action.Theme) },
                            { ActionPosition, Convert(action.Position) },
                            { ActionSize, Convert(action.Size) },
                        };
                        if (!string.IsNullOrWhiteSpace(action.Icon))
                            actionDict.Add(ActionIcon, action.Icon.Trim());
                        if (!string.IsNullOrEmpty(action.AttachToMember))
                            actionDict.Add(ActionMemberName, action.AttachToMember);
                        if (!string.IsNullOrEmpty(action.SectionName))
                            actionDict.Add(SectionName, action.SectionName);
                        if (action.HasToggleCondition)
                        {
                            actionDict.Add(ActionVisibilityToggle, new Dictionary<string, object?>
                            {
                                { "path", action.ToggleWhenMemberIsSet },
                                { "value", Convert(action.ToggleWhenSetTo, _currentType!) },
                                { "inverseCondition", action.InverseToggleCondition },
                            });
                        }
                        else
                        {
                            actionDict.Add(ActionVisibilityToggle, null);
                        }
                        if (action.HasDisableCondition)
                        {
                            actionDict.Add(ActionDisableToggle, new Dictionary<string, object?>
                            {
                                { "path", action.DisableWhenMemberIsSet },
                                { "value", Convert(action.DisableWhenSetTo, _currentType!) },
                                { "inverseCondition", action.InverseDisableCondition },
                            });
                        }
                        else
                        {
                            actionDict.Add(ActionDisableToggle, null);
                        }
                        actionDict.Add(ActionDisableIfNoChanges, action.DisableIfNoChanges);
                        actionList.Add(KeyValuePair.Create(methodInfo.Name, actionDict));
                    }
                    else if (methodInfo.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: var actionType, ReactiveEventType: var eventType })
                    {
                        var actionDict = new Dictionary<string, object?>
                        {
                            { ActionIdentity, methodInfo.Name },
                            { ActionName, title },
                            { ActionDescription, description },
                            { ActionType, Convert(actionType) },
                            { ActionEventType, Convert(eventType) },
                        };
                        reactiveActionList.Add(actionDict);
                    }
                }
                uiDict.Add(ElementHideSaveAction, hideSaveAction);
                uiDict.Add(ElementShowSaveAction, showSaveAction);
                uiDict.Add(ElementActions, actionList.ToDictionary());
                uiDict.Add(ElementReactiveActions, reactiveActionList);
            }
        }
    }

    private bool IsNewtonsoftJson() => _currentType!.IsAssignableTo(typeof(INewtonsoftJsonConfiguration));

    private bool IsIgnored(MemberInfo memberInfo) =>
        IsNewtonsoftJson()
            ? memberInfo.CustomAttributes.Any(a => a.AttributeType == typeof(Newtonsoft.Json.JsonIgnoreAttribute))
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
    private JToken? Convert(object? value, Type type)
        => value is null ? null : type is null || type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JToken.Parse(JsonConvert.SerializeObject(value, Formatting.None, _newtonsoftJsonSerializerSettings))
            : JToken.Parse(System.Text.Json.JsonSerializer.Serialize(value, _systemTextJsonSerializerOptions));

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
