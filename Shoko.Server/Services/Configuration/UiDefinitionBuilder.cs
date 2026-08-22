using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Joins a finished <see cref="JsonSchema"/> with the typed builders
///   <see cref="ShokoJsonSchemaGenerator"/> filled in while producing it, into
///   a <see cref="UiDefinition"/> a client can render from on its own.
/// </summary>
/// <remarks>
///   <para>
///     Schema-derived facts — type, minimum, maximum, default, required,
///     nullable — come off the schema node. Presentation and behaviour come off
///     the builder. Neither side re-parses the <c>x-uiDefinition</c> bag the
///     generator emits for <see cref="ShokoJsonSchemaValidator{TConfig}"/>.
///   </para>
/// </remarks>
/// <param name="logger">Logger.</param>
public class UiDefinitionBuilder(ILogger<UiDefinitionBuilder> logger)
{
    private readonly ILogger<UiDefinitionBuilder> _logger = logger;

    /// <summary>
    ///   Property names, in priority order, that the list-item primary label is
    ///   looked up under when the primary key resolves to an object.
    /// </summary>
    private static readonly string[] _titleKeyCandidates = ["title", "id", "value", "name", "label", "primary"];

    /// <summary>
    ///   Property names, in priority order, that the list-item secondary label
    ///   is looked up under when the primary key resolves to an object.
    /// </summary>
    private static readonly string[] _categoryKeyCandidates = ["category", "type", "key", "group", "secondary"];

    /// <summary>
    ///   Builds the definition for a generated schema, whether the schema
    ///   describes a configuration or an executable action's parameters.
    /// </summary>
    /// <param name="id">The id of whatever the schema describes.</param>
    /// <param name="name">The display name of whatever the schema describes.</param>
    /// <param name="description">An optional description of whatever the schema describes.</param>
    /// <param name="wrapped">The generated schema and its typed builders.</param>
    /// <returns>A definition that is self-sufficient for rendering.</returns>
    public UiDefinition Build(Guid id, string name, string? description, WrappedJsonSchema wrapped)
    {
        ArgumentNullException.ThrowIfNull(wrapped);

        var state = new WalkState(wrapped);
        var root = BuildElement(state, wrapped.Schema, null, null, isRoot: true, isRequired: true);
        while (state.PendingDefinitions.Count > 0)
        {
            var (definitionName, definitionSchema) = state.PendingDefinitions.Dequeue();
            if (state.Definitions.ContainsKey(definitionName))
                continue;

            // Reserve the slot before recursing so any nested revisit resolves
            // to a reference instead of recursing forever, and enter the walk
            // below the cycle check so this one occurrence is inlined.
            state.Definitions[definitionName] = new UiReferenceElement { Reference = definitionName };
            var resolvedDefinition = definitionSchema.ActualTypeSchema;
            state.OnStack.Add(resolvedDefinition);
            try
            {
                // A hoisted definition is reached through a reference, never as
                // the configuration's own root, so it never renders the
                // built-in save action.
                state.Definitions[definitionName] =
                    BuildElementCore(state, definitionSchema, resolvedDefinition, null, null, isRoot: false, isRequired: true);
            }
            finally
            {
                state.OnStack.Remove(resolvedDefinition);
            }
        }

        if (state.Definitions.Count > 0)
            _logger.LogInformation(
                "{Name} needed {Count} hoisted definition(s) because its element tree recurses: {Names}.",
                name,
                state.Definitions.Count,
                string.Join(", ", state.Definitions.Keys)
            );

        return new UiDefinition
        {
            ID = id,
            Name = name,
            Description = string.IsNullOrEmpty(description) ? null : description,
            Root = root,
            Definitions = state.Definitions,
        };
    }

    #region Element walk

    private UiElement BuildElement(
        WalkState state,
        JsonSchema declared,
        UiPropertyBuilder? property,
        string? key,
        bool isRoot,
        bool isRequired
    )
    {
        var resolved = declared.ActualTypeSchema;
        var isObject = resolved.Type.HasFlag(JsonObjectType.Object) && resolved.Properties.Count > 0;
        if (isObject)
        {
            var definitionName = state.GetDefinitionName(resolved);
            if (state.OnStack.Contains(resolved) || state.Definitions.ContainsKey(definitionName))
            {
                state.PendingDefinitions.Enqueue((definitionName, resolved));
                return Populate(state, new UiReferenceElement { Reference = definitionName }, declared, resolved, property, key, isRequired);
            }
        }

        if (isObject)
            state.OnStack.Add(resolved);
        try
        {
            return BuildElementCore(state, declared, resolved, property, key, isRoot, isRequired);
        }
        finally
        {
            if (isObject)
                state.OnStack.Remove(resolved);
        }
    }

    private UiElement BuildElementCore(
        WalkState state,
        JsonSchema declared,
        JsonSchema resolved,
        UiPropertyBuilder? property,
        string? key,
        bool isRoot,
        bool isRequired
    )
    {
        var elementType = property?.ElementType ?? DisplayElementType.Auto;
        if (elementType is DisplayElementType.Auto)
            elementType = ResolveAutoElementType(resolved);

        UiElement element = elementType switch
        {
            DisplayElementType.SectionContainer => BuildSectionContainer(state, resolved, isRoot),
            DisplayElementType.List => BuildList(state, resolved, property?.Element as UiListElementBuilder),
            DisplayElementType.Record => BuildRecord(state, resolved, property?.Element as UiRecordElementBuilder),
            DisplayElementType.Select when property?.Element is UiSelectElementBuilder select => new UiSelectElement
            {
                SelectType = select.SelectType,
                MultipleItems = select.MultipleItems,
            },
            DisplayElementType.Enum => new UiEnumElement
            {
                Values = ReadEnumValues(property?.Element as UiEnumElementBuilder, resolved),
                IsFlag = (property?.Element as UiEnumElementBuilder)?.IsFlag ?? false,
            },
            DisplayElementType.CodeBlock => new UiCodeEditorElement
            {
                Language = (property?.Element as UiCodeEditorElementBuilder)?.Language ?? CodeEditorLanguage.PlainText,
                AutoFormatOnLoad = (property?.Element as UiCodeEditorElementBuilder)?.AutoFormatOnLoad ?? false,
                MinLength = resolved.MinLength,
                MaxLength = resolved.MaxLength,
                Pattern = resolved.Pattern,
                Format = resolved.Format,
            },
            DisplayElementType.TextArea => new UiTextAreaElement
            {
                MinLength = resolved.MinLength,
                MaxLength = resolved.MaxLength,
                Pattern = resolved.Pattern,
                Format = resolved.Format,
            },
            DisplayElementType.Password => new UiPasswordElement
            {
                MinLength = resolved.MinLength,
                MaxLength = resolved.MaxLength,
                Pattern = resolved.Pattern,
                Format = resolved.Format,
            },
            _ => BuildPrimitive(resolved),
        };

        return Populate(state, element, declared, resolved, property, key, isRequired);
    }

    /// <summary>
    ///   Resolves the authored <c>auto</c> element type against the schema so
    ///   the wire format only ever carries concrete element kinds.
    /// </summary>
    private static DisplayElementType ResolveAutoElementType(JsonSchema resolved)
    {
        if (resolved.IsEnumeration)
            return DisplayElementType.Enum;
        if (resolved.Type.HasFlag(JsonObjectType.Array))
            return DisplayElementType.List;
        if (resolved.AdditionalPropertiesSchema is not null)
            return DisplayElementType.Record;
        if (resolved.Type.HasFlag(JsonObjectType.Object) && resolved.Properties.Count > 0)
            return DisplayElementType.SectionContainer;
        return DisplayElementType.Auto;
    }

    private static UiElement BuildPrimitive(JsonSchema resolved)
    {
        if (resolved.Type.HasFlag(JsonObjectType.Boolean))
            return new UiBooleanElement();
        if (resolved.Type.HasFlag(JsonObjectType.Integer))
            return new UiIntegerElement
            {
                Minimum = resolved.Minimum is { } min ? (long)min : null,
                Maximum = resolved.Maximum is { } max ? (long)max : null,
            };
        if (resolved.Type.HasFlag(JsonObjectType.Number))
            return new UiFloatElement
            {
                Minimum = resolved.Minimum is { } min ? (double)min : null,
                Maximum = resolved.Maximum is { } max ? (double)max : null,
            };
        if (resolved.Type.HasFlag(JsonObjectType.String))
            return new UiStringElement
            {
                MinLength = resolved.MinLength,
                MaxLength = resolved.MaxLength,
                Pattern = resolved.Pattern,
                Format = resolved.Format,
            };
        return new UiUnknownElement { SchemaType = resolved.Type.ToString() };
    }

    private UiElement BuildSectionContainer(WalkState state, JsonSchema resolved, bool isRoot)
    {
        var classBuilder = state.GetClass(resolved);
        // Kept as a list as well as a lookup, so the fallback pass below walks
        // the schema in schema order rather than in hash order.
        var properties = resolved.Properties
            .Where(kv => !string.Equals(kv.Key, "$schema", StringComparison.Ordinal))
            .ToList();
        var propertyLookup = properties.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        // The structure names CLR members while the schema names JSON
        // properties, and the serializer is free to rename one into the other.
        var propertyNames = classBuilder?.Properties
            .GroupBy(x => x.MemberName, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().PropertyName, StringComparer.Ordinal);
        // Insertion-ordered so the two maps enumerate in structure order, and a
        // client can render straight off `Items` without walking `Structure`.
        var uiItems = new OrderedDictionary<string, UiElement>(StringComparer.Ordinal);
        var actions = new OrderedDictionary<string, UiAction>(StringComparer.Ordinal);
        var structure = new List<UiStructureEntry>();
        var placedProperties = new HashSet<string>(StringComparer.Ordinal);
        var placedActions = new HashSet<string>(StringComparer.Ordinal);

        // The authored member order drives both lists, so neither one rides on
        // a dictionary's insertion order any more.
        foreach (var entry in classBuilder?.Structure ?? [])
        {
            if (entry.MemberType is UiMemberKind.Method)
            {
                if (classBuilder!.Actions.Find(x => string.Equals(x.ID, entry.Name, StringComparison.Ordinal)) is not { } action || !placedActions.Add(action.ID))
                    continue;

                actions.Add(action.ID, ReadAction(state, action));
                structure.Add(new UiStructureEntry { Name = action.ID, Kind = UiStructureMemberKind.Action });
                continue;
            }

            var propertyName = propertyNames!.GetValueOrDefault(entry.Name) ?? entry.Name;
            if (!propertyLookup.TryGetValue(propertyName, out var propertySchema) || !placedProperties.Add(propertyName))
                continue;

            uiItems.Add(propertyName, BuildItem(state, classBuilder, propertyName, propertySchema));
            structure.Add(new UiStructureEntry { Name = propertyName, Kind = UiStructureMemberKind.Item });
        }

        // Anything the structure did not mention — inherited members, members
        // the serializer renamed — keeps its schema order at the end.
        foreach (var (propertyName, propertySchema) in properties)
        {
            if (!placedProperties.Add(propertyName))
                continue;

            uiItems.Add(propertyName, BuildItem(state, classBuilder, propertyName, propertySchema));
            structure.Add(new UiStructureEntry { Name = propertyName, Kind = UiStructureMemberKind.Item });
        }
        foreach (var action in classBuilder?.Actions ?? [])
        {
            if (!placedActions.Add(action.ID))
                continue;

            actions.Add(action.ID, ReadAction(state, action));
            structure.Add(new UiStructureEntry { Name = action.ID, Kind = UiStructureMemberKind.Action });
        }

        return new UiSectionContainerElement
        {
            SectionType = classBuilder?.SectionType ?? DisplaySectionType.FieldSet,
            DefaultSectionName = classBuilder?.SectionName ?? "Default",
            AppendFloatingSectionsAtEnd = classBuilder?.AppendFloatingSectionsAtEnd ?? false,
            // Only the configuration's own root renders the built-in save
            // action, and there it is on unless the class opted out.
            ShowSaveAction = classBuilder is not null && (classBuilder.ShowSaveAction || (isRoot && !classBuilder.HideSaveAction)),
            PrimaryKey = classBuilder?.PrimaryKey,
            Items = uiItems,
            Actions = actions,
            Structure = structure,
        };
    }

    private UiElement BuildItem(WalkState state, UiClassBuilder? classBuilder, string propertyName, JsonSchemaProperty propertySchema)
    {
        // Mirrors how the generator files a property's builder: a collection
        // property produces one builder for the collection node and one for the
        // element node, and only the former carries the suffix.
        var propertyKey = propertyName;
        if (propertySchema.Item is not null)
            propertyKey += "+List";
        if (propertySchema.AdditionalPropertiesSchema is not null)
            propertyKey += "+Dict";
        return BuildElement(state, propertySchema, classBuilder?.GetProperty(propertyKey), propertyName, isRoot: false, propertySchema.IsRequired);
    }

    private UiElement BuildList(WalkState state, JsonSchema resolved, UiListElementBuilder? list)
    {
        var itemSchema = resolved.Item ?? resolved.Items.FirstOrDefault() ?? new JsonSchema();
        var itemElement = BuildElement(state, itemSchema, list?.Item, null, isRoot: false, isRequired: true);
        var (titlePath, categoryPath) = ResolveItemLabelPaths(itemElement);
        return new UiListElement
        {
            ListType = list?.ListType ?? DisplayListType.Auto,
            Item = itemElement,
            Sortable = list?.Sortable ?? true,
            UniqueItems = list?.UniqueItems ?? false,
            HideAddAction = list?.HideAddAction ?? false,
            HideRemoveAction = list?.HideRemoveAction ?? false,
            MinItems = resolved.MinItems > 0 ? resolved.MinItems : null,
            MaxItems = resolved.MaxItems > 0 ? resolved.MaxItems : null,
            ItemTitlePath = titlePath,
            ItemCategoryPath = categoryPath,
        };
    }

    private UiElement BuildRecord(WalkState state, JsonSchema resolved, UiRecordElementBuilder? record)
    {
        var valueSchema = resolved.AdditionalPropertiesSchema ?? new JsonSchema();
        return new UiRecordElement
        {
            RecordType = record?.RecordType ?? DisplayRecordType.Auto,
            KeyItem = BuildKeyElement(record),
            // The generator funnels a record's key type and value type through
            // the same property key, so the inner builder only describes the
            // value when it is not the key that landed there.
            Item = BuildElement(state, valueSchema, record is { DescribesValue: true } ? record.Item : null, null, isRoot: false, isRequired: true),
            Sortable = record?.Sortable ?? true,
            HideAddAction = record?.HideAddAction ?? false,
            HideRemoveAction = record?.HideRemoveAction ?? false,
        };
    }

    /// <summary>
    ///   Builds the element the client edits a record's keys with, off the CLR
    ///   key type. The schema does not describe the key, which is why this used
    ///   to always come out as free text.
    /// </summary>
    private static UiElement BuildKeyElement(UiRecordElementBuilder? record)
    {
        var keyType = record?.KeyType is { } type ? Nullable.GetUnderlyingType(type) ?? type : null;
        UiElement element = keyType switch
        {
            { IsEnum: true } when record!.KeyEnumValues is { } enumValues => new UiEnumElement
            {
                Values = enumValues.Select(ToEnumValue).ToList(),
                IsFlag = record.KeyEnumIsFlag,
            },
            not null when keyType == typeof(bool) => new UiBooleanElement(),
            not null when keyType == typeof(byte) || keyType == typeof(sbyte) || keyType == typeof(short) || keyType == typeof(ushort) ||
                keyType == typeof(int) || keyType == typeof(uint) || keyType == typeof(long) || keyType == typeof(ulong) => new UiIntegerElement(),
            not null when keyType == typeof(float) || keyType == typeof(double) || keyType == typeof(decimal) => new UiFloatElement(),
            not null when keyType == typeof(Guid) => new UiStringElement { Format = "guid" },
            _ => new UiStringElement(),
        };
        element.Label = "Key";
        element.IsRequired = true;
        return element;
    }

    /// <summary>
    ///   Computes the paths the client should read an item's primary and
    ///   secondary label from, replacing the field-name guessing the client
    ///   does today.
    /// </summary>
    private static (string? TitlePath, string? CategoryPath) ResolveItemLabelPaths(UiElement itemElement)
    {
        if (itemElement is not UiSectionContainerElement { PrimaryKey: { Length: > 0 } primaryKey } container)
            return (null, null);

        if (!container.Items.TryGetValue(primaryKey, out var keyElement))
            return (primaryKey, null);

        if (keyElement is not UiSectionContainerElement keyContainer)
            return (primaryKey, null);

        var itemKeys = keyContainer.Items.Keys.ToList();
        var title = _titleKeyCandidates
            .Select(candidate => itemKeys.FirstOrDefault(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(x => x is not null);
        var category = _categoryKeyCandidates
            .Select(candidate => itemKeys.FirstOrDefault(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(x => x is not null);
        return (title is null ? primaryKey : $"{primaryKey}.{title}", category is null ? null : $"{primaryKey}.{category}");
    }

    private static UiElement Populate(
        WalkState state,
        UiElement element,
        JsonSchema declared,
        JsonSchema resolved,
        UiPropertyBuilder? property,
        string? key,
        bool isRequired
    )
    {
        // A collection's items and a record's keys and values are reached
        // through the same property definition as their container, but the
        // property-level decorations belong to the container alone.
        var isNamedMember = key is not null;
        element.Key = key;
        element.Label = declared.Title ?? resolved.Title ?? (isNamedMember ? property?.Label : null) ?? key ?? string.Empty;
        element.Description = string.IsNullOrEmpty(declared.Description) ? resolved.Description : declared.Description;
        element.Size = property?.ElementSize ?? DisplayElementSize.Normal;
        element.Visibility = ReadVisibility(state, isNamedMember ? property?.Visibility : null);
        element.Badge = isNamedMember && property?.Badge is { } badge ? new UiBadge { Name = badge.Name, Theme = badge.Theme } : null;
        element.RequiresRestart = isNamedMember && (property?.RequiresRestart ?? false);
        element.EnvironmentVariable = isNamedMember && property?.EnvironmentVariable is { Length: > 0 } envVar
            ? new UiEnvironmentVariable { Name = envVar, AllowOverride = property!.EnvironmentVariableOverridable }
            : null;
        element.SectionName = isNamedMember ? property?.SectionName : null;
        element.Default = ToToken(declared.Default ?? resolved.Default);
        element.IsRequired = isRequired;
        element.IsNullable = declared.IsNullable(SchemaType.JsonSchema) || resolved.IsNullable(SchemaType.JsonSchema);
        element.DeniedValues = property?.DeniedValues?.Select(state.ConvertToken).ToList();
        return element;
    }

    #endregion

    #region Builder readers

    private static UiVisibility ReadVisibility(WalkState state, UiVisibilityBuilder? visibility)
        => visibility is null
            ? new UiVisibility()
            : new UiVisibility
            {
                Default = visibility.Default,
                Advanced = visibility.Advanced,
                Toggle = visibility.Toggle is { } toggle
                    ? new UiVisibilityCondition
                    {
                        Path = toggle.Path,
                        Value = state.ConvertToken(toggle.Value),
                        Visibility = toggle.Visibility ?? DisplayVisibility.Visible,
                        InverseCondition = toggle.InverseCondition,
                    }
                    : null,
                Disable = ReadCondition(state, visibility.Disable),
            };

    private static IReadOnlyList<UiEnumValue> ReadEnumValues(UiEnumElementBuilder? element, JsonSchema resolved)
    {
        if (element is not null)
            return element.Values.Select(ToEnumValue).ToList();

        // No builder describes this node, so fall back to whatever the schema
        // itself lists rather than emitting a choice with no choices.
        return resolved.Enumeration.OfType<string>().Select(x => new UiEnumValue { Title = x, Value = x }).ToList();
    }

    private static UiEnumValue ToEnumValue(UiEnumValueBuilder value)
        => new()
        {
            Title = value.Title,
            Description = string.IsNullOrEmpty(value.Description) ? null : value.Description,
            Value = value.Value,
            // carried through so a renderer can surface every name a value is
            // known by; a document may legitimately use one instead of Value
            Alias = string.IsNullOrEmpty(value.Alias) ? null : value.Alias,
            AliasValues = string.IsNullOrEmpty(value.AliasValues) ? null : value.AliasValues,
        };

    private static UiAction ReadAction(WalkState state, UiActionBuilder action)
        => new()
        {
            ID = action.ID,
            Title = string.IsNullOrEmpty(action.Title) ? action.ID : action.Title,
            Description = string.IsNullOrEmpty(action.Description) ? null : action.Description,
            Theme = action.Theme,
            Position = action.Position,
            Size = action.Size,
            Icon = action.Icon,
            SectionName = action.SectionName,
            MemberName = action.MemberName,
            Toggle = ReadCondition(state, action.Toggle),
            Disable = ReadCondition(state, action.Disable),
            DisableIfNoChanges = action.DisableIfNoChanges,
        };

    private static UiCondition? ReadCondition(WalkState state, UiConditionBuilder? condition)
        => condition is null
            ? null
            : new UiCondition
            {
                Path = condition.Path,
                Value = state.ConvertToken(condition.Value),
                InverseCondition = condition.InverseCondition,
            };

    private static JToken? ToToken(object? value)
        => value switch
        {
            null => null,
            JToken token => token,
            _ => JToken.FromObject(value),
        };

    #endregion

    /// <summary>
    ///   Mutable bookkeeping for a single walk.
    /// </summary>
    private sealed class WalkState(WrappedJsonSchema wrapped)
    {
        private readonly Dictionary<JsonSchema, string> _names = wrapped.Schema.Definitions
            .ToDictionary(kv => kv.Value.ActualSchema, kv => kv.Key);

        public HashSet<JsonSchema> OnStack { get; } = [];

        public Dictionary<string, UiElement> Definitions { get; } = new(StringComparer.Ordinal);

        public Queue<(string Name, JsonSchema Schema)> PendingDefinitions { get; } = new();

        public UiClassBuilder? GetClass(JsonSchema schema)
            => wrapped.UiBuilders.TryGetValue(schema, out var builder) ? builder : null;

        public JToken? ConvertToken(object? value)
            => wrapped.EmitContext is { } context ? context.ConvertToken(value) : ToToken(value);

        public string GetDefinitionName(JsonSchema schema)
            => _names.TryGetValue(schema, out var name) ? name : schema.Title ?? "Anonymous";
    }
}
