using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NJsonSchema;
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
            DisplayElementType.SectionContainer => BuildSectionContainer(
                state,
                resolved,
                isRoot,
                ResolveLabel(declared, resolved, property, key),
                ResolveDescription(declared, resolved)
            ),
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

    private UiElement BuildSectionContainer(WalkState state, JsonSchema resolved, bool isRoot, string label, string? description)
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
        // Insertion-ordered so the two maps enumerate in authored order.
        var uiItems = new OrderedDictionary<string, UiElement>(StringComparer.Ordinal);
        var actions = new OrderedDictionary<string, UiAction>(StringComparer.Ordinal);
        var members = new List<SectionMember>();
        var placedProperties = new HashSet<string>(StringComparer.Ordinal);
        var placedActions = new HashSet<string>(StringComparer.Ordinal);

        // The authored member order drives both maps and the sections, so none
        // of them rides on a dictionary's insertion order.
        foreach (var entry in classBuilder?.Structure ?? [])
        {
            if (entry.MemberType is UiMemberKind.Method)
            {
                if (classBuilder!.Actions.Find(x => string.Equals(x.ID, entry.Name, StringComparison.Ordinal)) is not { } action || !placedActions.Add(action.ID))
                    continue;

                actions.Add(action.ID, ReadAction(state, action));
                members.Add(new(new UiStructureEntry { Name = action.ID, Kind = UiStructureMemberKind.Action }, action.SectionName, null, action.Position));
                continue;
            }

            var propertyName = propertyNames!.GetValueOrDefault(entry.Name) ?? entry.Name;
            if (!propertyLookup.TryGetValue(propertyName, out var propertySchema) || !placedProperties.Add(propertyName))
                continue;

            members.Add(AddItem(state, classBuilder, uiItems, propertyName, propertySchema));
        }

        // Anything the structure did not mention — inherited members, members
        // the serializer renamed — keeps its schema order at the end.
        foreach (var (propertyName, propertySchema) in properties)
        {
            if (!placedProperties.Add(propertyName))
                continue;

            members.Add(AddItem(state, classBuilder, uiItems, propertyName, propertySchema));
        }
        foreach (var action in classBuilder?.Actions ?? [])
        {
            if (!placedActions.Add(action.ID))
                continue;

            actions.Add(action.ID, ReadAction(state, action));
            members.Add(new(new UiStructureEntry { Name = action.ID, Kind = UiStructureMemberKind.Action }, action.SectionName, null, action.Position));
        }

        var sectionType = classBuilder?.SectionType ?? DisplaySectionType.FieldSet;
        var layout = BuildLayout(members, classBuilder, sectionType, label);
        return new UiSectionContainerElement
        {
            SectionType = sectionType,
            // Only the configuration's own root renders the built-in save
            // action, and there it is on unless the class opted out.
            ShowSaveAction = classBuilder is not null && (classBuilder.ShowSaveAction || (isRoot && !classBuilder.HideSaveAction)),
            PrimaryKey = classBuilder?.PrimaryKey,
            Items = uiItems,
            Actions = actions,
            FloatingSections = layout.FloatingSections,
            StartActions = layout.StartActions,
            EndActions = layout.EndActions,
            Structure = layout.Structure,
        };
    }

    private SectionMember AddItem(
        WalkState state,
        UiClassBuilder? classBuilder,
        OrderedDictionary<string, UiElement> items,
        string propertyName,
        JsonSchemaProperty propertySchema
    )
    {
        // Mirrors how the generator files a property's builder: a collection
        // property produces one builder for the collection node and one for the
        // element node, and only the former carries the suffix.
        var propertyKey = propertyName;
        if (propertySchema.Item is not null)
            propertyKey += "+List";
        if (propertySchema.AdditionalPropertiesSchema is not null)
            propertyKey += "+Dict";
        var property = classBuilder?.GetProperty(propertyKey);
        var element = BuildElement(state, propertySchema, property, propertyName, isRoot: false, propertySchema.IsRequired);
        items.Add(propertyName, element);
        return new(new UiStructureEntry { Name = propertyName, Kind = UiStructureMemberKind.Item }, property?.SectionName, element, null);
    }

    /// <summary>
    ///   Lays a container's members out: the order they render in, the groups
    ///   assembled out of them, and the actions pinned outside those groups.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Members render in the order they were authored, so a nested container
    ///     stays an item where it stands rather than being swept to one end.
    ///     Members sharing a section name are gathered into one floating section,
    ///     entered at the first of them, and <c>AppendFloatingSectionsAtEnd</c>
    ///     moves every gathered section past the rest.
    ///   </para>
    ///   <para>
    ///     A member with no section name keeps its place, unless the container is
    ///     laid out as tabs or the class named its default section: a tab has to
    ///     have a label, so there the loose members are gathered too. A nested
    ///     container is never gathered — it labels itself.
    ///   </para>
    /// </remarks>
    private static ContainerLayout BuildLayout(List<SectionMember> members, UiClassBuilder? classBuilder, DisplaySectionType sectionType, string label)
    {
        var gatherLooseMembers = classBuilder?.SectionName is not null || sectionType is DisplaySectionType.Tab;
        var defaultTitle = classBuilder?.SectionName ?? (string.IsNullOrEmpty(label) ? "Default" : label);
        var structure = new List<UiStructureEntry>();
        var drafts = new OrderedDictionary<string, FloatingSectionDraft>(StringComparer.Ordinal);
        var startActions = new List<string>();
        var endActions = new List<string>();
        foreach (var member in members)
        {
            var title = member.SectionName ?? (gatherLooseMembers && !IsSelfLabelling(member) ? defaultTitle : null);
            if (title is null)
            {
                Place(member, structure, startActions, endActions);
                continue;
            }

            if (!drafts.TryGetValue(title, out var draft))
            {
                drafts.Add(title, draft = new FloatingSectionDraft(title));
                structure.Add(new UiStructureEntry { Name = title, Kind = UiStructureMemberKind.FloatingSection });
            }
            Place(member, draft.Structure, draft.StartActions, draft.EndActions);
        }

        if (classBuilder?.AppendFloatingSectionsAtEnd ?? false)
            structure =
            [
                .. structure.Where(x => x.Kind is not UiStructureMemberKind.FloatingSection),
                .. structure.Where(x => x.Kind is UiStructureMemberKind.FloatingSection),
            ];

        var floatingSections = new OrderedDictionary<string, UiFloatingSection>(StringComparer.Ordinal);
        foreach (var (title, draft) in drafts)
            floatingSections.Add(title, draft.ToSection());
        return new(structure, floatingSections, startActions, endActions);
    }

    /// <summary>
    ///   Files a member where its authored position says: an action pinned to
    ///   one end of what holds it, anything else in the render order.
    /// </summary>
    private static void Place(SectionMember member, List<UiStructureEntry> structure, List<string> startActions, List<string> endActions)
    {
        switch (member.Position)
        {
            case DisplayButtonPosition.Start:
                startActions.Add(member.Entry.Name);
                break;
            case DisplayButtonPosition.End:
                endActions.Add(member.Entry.Name);
                break;
            default:
                structure.Add(member.Entry);
                break;
        }
    }

    /// <summary>
    ///   Whether a member renders its own heading, and so is never gathered into
    ///   the default section of a container that needs one.
    /// </summary>
    private static bool IsSelfLabelling(SectionMember member)
        => member.Element is { } element && (IsContainer(element) || (element is UiListElement list && IsContainer(list.Item)));

    /// <remarks>
    ///   A reference only ever stands in for an object with properties that
    ///   recursed, and those are always built as section containers.
    /// </remarks>
    private static bool IsContainer(UiElement element)
        => element is UiSectionContainerElement or UiReferenceElement;

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
        element.Label = ResolveLabel(declared, resolved, property, key);
        element.Description = ResolveDescription(declared, resolved);
        element.Size = property?.ElementSize ?? DisplayElementSize.Normal;
        element.Visibility = ReadVisibility(state, isNamedMember ? property?.Visibility : null);
        element.Badge = isNamedMember && property?.Badge is { } badge ? new UiBadge { Name = badge.Name, Theme = badge.Theme } : null;
        element.RequiresRestart = isNamedMember && (property?.RequiresRestart ?? false);
        element.EnvironmentVariable = isNamedMember && property?.EnvironmentVariable is { Length: > 0 } envVar
            ? new UiEnvironmentVariable { Name = envVar, AllowOverride = property!.EnvironmentVariableOverridable }
            : null;
        element.Default = ToToken(declared.Default ?? resolved.Default);
        element.IsRequired = isRequired;
        element.IsNullable = declared.IsNullable(SchemaType.JsonSchema) || resolved.IsNullable(SchemaType.JsonSchema);
        element.DeniedValues = property?.DeniedValues?.Select(state.ConvertToken).ToList();
        return element;
    }

    private static string ResolveLabel(JsonSchema declared, JsonSchema resolved, UiPropertyBuilder? property, string? key)
        => declared.Title ?? resolved.Title ?? (key is not null ? property?.Label : null) ?? key ?? string.Empty;

    private static string? ResolveDescription(JsonSchema declared, JsonSchema resolved)
        => string.IsNullOrEmpty(declared.Description) ? resolved.Description : declared.Description;

    /// <summary>
    ///   A container member on its way into a section.
    /// </summary>
    /// <param name="Entry">The entry the section lists.</param>
    /// <param name="SectionName">The authored section name, if any.</param>
    /// <param name="Element">The built element, or <c>null</c> for an action.</param>
    /// <param name="Position">Where an action's button is pinned, if it is one.</param>
    private sealed record SectionMember(UiStructureEntry Entry, string? SectionName, UiElement? Element, DisplayButtonPosition? Position);

    /// <summary>
    ///   Everything a container needs to say about how its members render.
    /// </summary>
    /// <param name="Structure">The container's own render order.</param>
    /// <param name="FloatingSections">The groups assembled out of its members.</param>
    /// <param name="StartActions">Actions pinned to the top, outside every group.</param>
    /// <param name="EndActions">Actions pinned to the bottom, outside every group.</param>
    private sealed record ContainerLayout(
        IReadOnlyList<UiStructureEntry> Structure,
        IReadOnlyDictionary<string, UiFloatingSection> FloatingSections,
        IReadOnlyList<string> StartActions,
        IReadOnlyList<string> EndActions
    );

    /// <summary>
    ///   A floating section being assembled.
    /// </summary>
    /// <param name="Title">The section's title.</param>
    private sealed record FloatingSectionDraft(string Title)
    {
        public List<UiStructureEntry> Structure { get; } = [];

        public List<string> StartActions { get; } = [];

        public List<string> EndActions { get; } = [];

        public UiFloatingSection ToSection()
            => new() { Title = Title, StartActions = StartActions, EndActions = EndActions, Structure = Structure };
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
            Size = action.Size,
            Icon = action.Icon,
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
