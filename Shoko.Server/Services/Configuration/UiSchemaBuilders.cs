using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Services.Configuration;

// The types in this file are the typed accumulators
// `ShokoJsonSchemaGenerator` fills in while it walks a configuration type.
// They replace the `Dictionary<string, object?>` bags the generator used to
// build up. `UiDefinition` is built off these builders, and so is the one bag
// still written onto the schema — `x-uiDefinition`, now cut down to the five
// entries `ShokoJsonSchemaValidator<TConfig>` reads back during validation.
// Nothing presentational is emitted there any more.

/// <summary>
///   The value converters a configuration's own serialiser would use, handed to
///   the emitters so a bag entry serialises the same way it always has.
/// </summary>
internal sealed record UiEmitContext
{
    /// <summary>
    ///   Serialises a value the way the configuration's own serialiser would.
    /// </summary>
    public required Func<object?, object?> Convert { get; init; }

    /// <summary>
    ///   Serialises a value into a <see cref="JToken"/> the way the
    ///   configuration's own serialiser would.
    /// </summary>
    public required Func<object?, JToken?> ConvertToken { get; init; }
}

/// <summary>
///   Whether a member listed in a container's structure is a value the user
///   edits or an action the user invokes.
/// </summary>
internal enum UiMemberKind
{
    /// <summary>A property or field.</summary>
    Property = 0,

    /// <summary>A method exposed as an action.</summary>
    Method = 1,
}

/// <summary>
///   One entry in a container's authored member order.
/// </summary>
/// <param name="Name">The CLR member name.</param>
/// <param name="MemberType">What kind of member it is.</param>
internal sealed record UiMemberOrderEntry(string Name, UiMemberKind MemberType)
{
    /// <summary>
    ///   The wire value used in the <c>structure</c> bag.
    /// </summary>
    public string Value => MemberType is UiMemberKind.Method ? "method" : "property";
}

/// <summary>
///   A condition evaluated against another member of the same configuration.
/// </summary>
internal sealed class UiConditionBuilder
{
    /// <summary>The path to the member to compare against.</summary>
    public required string Path { get; init; }

    /// <summary>The value the member has to hold.</summary>
    public required object? Value { get; init; }

    /// <summary>The visibility to switch to, for visibility toggles only.</summary>
    public DisplayVisibility? Visibility { get; init; }

    /// <summary>Whether the comparison is inverted.</summary>
    public required bool InverseCondition { get; init; }
}

/// <summary>
///   When and whether an element is shown and editable.
/// </summary>
internal sealed class UiVisibilityBuilder
{
    /// <summary>The visibility applied when no condition holds.</summary>
    public required DisplayVisibility Default { get; init; }

    /// <summary>Whether the element is advanced-mode only.</summary>
    public required bool Advanced { get; init; }

    /// <summary>The condition that switches the visibility, if any.</summary>
    public UiConditionBuilder? Toggle { get; init; }

    /// <summary>The condition that disables the element, if any.</summary>
    public UiConditionBuilder? Disable { get; init; }
}

/// <summary>
///   A small labelled marker rendered next to an element's label.
/// </summary>
internal sealed class UiBadgeBuilder
{
    /// <summary>The text inside the badge.</summary>
    public required string Name { get; init; }

    /// <summary>The colour theme of the badge.</summary>
    public required DisplayColorTheme Theme { get; init; }
}

/// <summary>
///   One value of an enumeration, together with the aliases that collapsed onto
///   it because they share its underlying value.
/// </summary>
internal sealed class UiEnumValueBuilder
{
    /// <summary>The human-readable name of the value.</summary>
    public required string Title { get; init; }

    /// <summary>The description of the value.</summary>
    public required string Description { get; init; }

    /// <summary>The value as it appears in the configuration document.</summary>
    public required string Value { get; init; }

    /// <summary>The display names of the aliases that share this value.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The wire values of the aliases that share this value.</summary>
    public string AliasValues { get; set; } = string.Empty;

    /// <summary>Emits the entry the validator reads back.</summary>
    /// <remarks>
    ///   <see cref="ShokoJsonSchemaValidator{TConfig}"/> casts the emitted list
    ///   to <c>List&lt;Dictionary&lt;string, string&gt;&gt;</c>, so the concrete
    ///   types here are load-bearing.
    /// </remarks>
    public Dictionary<string, string> ToExtensionData()
        => new()
        {
            { "value", Value },
            { "aliasValues", AliasValues },
        };
}

/// <summary>
///   Base class for the per-element-kind part of a property's definition.
/// </summary>
internal abstract class UiElementBuilder
{
    /// <summary>The element kind this builder stands for.</summary>
    public abstract DisplayElementType ElementType { get; }
}

/// <summary>
///   A server-populated selection component.
/// </summary>
internal sealed class UiSelectElementBuilder : UiElementBuilder
{
    /// <inheritdoc />
    public override DisplayElementType ElementType => DisplayElementType.Select;

    /// <summary>How the options should be laid out.</summary>
    public required DisplaySelectType SelectType { get; init; }

    /// <summary>Whether more than one option may be selected.</summary>
    public required bool MultipleItems { get; init; }
}

/// <summary>
///   An ordered collection of items of a single element kind.
/// </summary>
internal sealed class UiListElementBuilder : UiElementBuilder
{
    /// <inheritdoc />
    public override DisplayElementType ElementType => DisplayElementType.List;

    /// <summary>How the list should be laid out.</summary>
    public required DisplayListType ListType { get; init; }

    /// <summary>Whether the user may reorder the items.</summary>
    public required bool Sortable { get; init; }

    /// <summary>Whether duplicate items are rejected.</summary>
    public required bool UniqueItems { get; init; }

    /// <summary>Whether the add-item affordance is suppressed.</summary>
    public required bool HideAddAction { get; init; }

    /// <summary>Whether the remove-item affordance is suppressed.</summary>
    public required bool HideRemoveAction { get; init; }

    /// <summary>The class definition of the item, when the item is a class.</summary>
    public UiClassBuilder? ItemClass { get; set; }

    /// <summary>The property definition the item contributed, if any.</summary>
    public UiPropertyBuilder? Item { get; set; }

    /// <summary>
    ///   The resolved element kind of the item, or <c>null</c> when nothing
    ///   overrode it.
    /// </summary>
    public DisplayElementType? ItemElementType { get; set; }
}

/// <summary>
///   A keyed collection of values of a single element kind.
/// </summary>
internal sealed class UiRecordElementBuilder : UiElementBuilder
{
    /// <inheritdoc />
    public override DisplayElementType ElementType => DisplayElementType.Record;

    /// <summary>How the record should be laid out.</summary>
    public required DisplayRecordType RecordType { get; init; }

    /// <summary>Whether the user may reorder the entries.</summary>
    public required bool Sortable { get; init; }

    /// <summary>Whether the add-entry affordance is suppressed.</summary>
    public required bool HideAddAction { get; init; }

    /// <summary>Whether the remove-entry affordance is suppressed.</summary>
    public required bool HideRemoveAction { get; init; }

    /// <summary>The CLR type of the record's keys.</summary>
    public required Type KeyType { get; init; }

    /// <summary>The CLR type of the record's values.</summary>
    public required Type ValueType { get; init; }

    /// <summary>
    ///   The key's selectable values, when the key is an enumeration.
    /// </summary>
    public IReadOnlyList<UiEnumValueBuilder>? KeyEnumValues { get; set; }

    /// <summary>Whether the key enumeration is a flags enumeration.</summary>
    public bool KeyEnumIsFlag { get; set; }

    /// <summary>The class definition of the value, when the value is a class.</summary>
    public UiClassBuilder? ValueClass { get; set; }

    /// <summary>
    ///   The property definition the record's inner type contributed. Note that
    ///   this may describe the <em>key</em> rather than the value; see
    ///   <see cref="DescribesValue"/>.
    /// </summary>
    public UiPropertyBuilder? Item { get; set; }

    /// <summary>
    ///   Whether <see cref="Item"/> describes the record's value. The generator
    ///   funnels both the key type and the value type through the same property
    ///   key, so for an enum-keyed record it is the key that lands there.
    /// </summary>
    public bool DescribesValue => Item is null || Item.SourceType != KeyType || KeyType == ValueType;

    /// <summary>
    ///   The resolved element kind of the value, or <c>null</c> when nothing
    ///   overrode it.
    /// </summary>
    public DisplayElementType? ValueElementType { get; set; }
}

/// <summary>
///   A choice between a fixed set of named values.
/// </summary>
internal sealed class UiEnumElementBuilder : UiElementBuilder
{
    /// <inheritdoc />
    public override DisplayElementType ElementType => DisplayElementType.Enum;

    /// <summary>The selectable values, in declaration order.</summary>
    public required IReadOnlyList<UiEnumValueBuilder> Values { get; init; }

    /// <summary>Whether the values are bit flags.</summary>
    public required bool IsFlag { get; init; }
}

/// <summary>
///   A syntax-highlighted code editor.
/// </summary>
internal sealed class UiCodeEditorElementBuilder : UiElementBuilder
{
    /// <inheritdoc />
    public override DisplayElementType ElementType => DisplayElementType.CodeBlock;

    /// <summary>The language to highlight the content as.</summary>
    public required CodeEditorLanguage Language { get; init; }

    /// <summary>Whether to reformat the content when it is first loaded.</summary>
    public required bool AutoFormatOnLoad { get; init; }
}

/// <summary>
///   A multi-line text input.
/// </summary>
internal sealed class UiTextAreaElementBuilder : UiElementBuilder
{
    /// <inheritdoc />
    public override DisplayElementType ElementType => DisplayElementType.TextArea;
}

/// <summary>
///   A masked text input.
/// </summary>
internal sealed class UiPasswordElementBuilder : UiElementBuilder
{
    /// <inheritdoc />
    public override DisplayElementType ElementType => DisplayElementType.Password;
}

/// <summary>
///   Everything the generator learned about a single property.
/// </summary>
internal sealed class UiPropertyBuilder
{
    /// <summary>
    ///   The key the generator files this definition under: the JSON property
    ///   name, suffixed with <c>+List</c> or <c>+Dict</c> for the collection
    ///   node of a collection property.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>The JSON property name.</summary>
    public required string PropertyName { get; init; }

    /// <summary>The CLR member name.</summary>
    public required string MemberName { get; init; }

    /// <summary>The CLR type this definition was built from.</summary>
    public required Type SourceType { get; init; }

    /// <summary>The property itself.</summary>
    public required PropertyInfo Property { get; init; }

    /// <summary>Whether the definition has already been populated.</summary>
    public bool IsPopulated { get; set; }

    /// <summary>The label of the element.</summary>
    public string? Label { get; set; }

    /// <summary>The authored group name, if any.</summary>
    public string? Group { get; set; }

    /// <summary>How much room the element should take up.</summary>
    public DisplayElementSize ElementSize { get; set; }

    /// <summary>Whether this property identifies its container.</summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>Values the property must not be set to.</summary>
    public IReadOnlyList<object?>? DeniedValues { get; set; }

    /// <summary>The section within the container this property belongs to.</summary>
    public string? SectionName { get; set; }

    /// <summary>When and whether the element is shown and editable.</summary>
    public UiVisibilityBuilder? Visibility { get; set; }

    /// <summary>Whether changing the property needs a restart.</summary>
    public bool RequiresRestart { get; set; }

    /// <summary>The environment variable backing this property, if any.</summary>
    public string? EnvironmentVariable { get; set; }

    /// <summary>Whether the loaded environment variable may be overridden.</summary>
    public bool EnvironmentVariableOverridable { get; set; }

    /// <summary>The badge to render next to the label, if any.</summary>
    public UiBadgeBuilder? Badge { get; set; }

    /// <summary>The kind-specific part of the definition, if any.</summary>
    public UiElementBuilder? Element { get; set; }

    /// <summary>
    ///   The element kind, or <see cref="DisplayElementType.Auto"/> when the
    ///   schema decides.
    /// </summary>
    public DisplayElementType ElementType => Element?.ElementType ?? DisplayElementType.Auto;

    /// <summary>
    ///   Emits the bag <see cref="ShokoJsonSchemaValidator{TConfig}"/> reads
    ///   back off the schema node, or <c>null</c> when there is nothing in it
    ///   for the validator to act on.
    /// </summary>
    /// <remarks>
    ///   Presentation lives in <see cref="Shoko.Abstractions.UI.UiDefinition"/>
    ///   now; the only reason a bag is still written onto the schema is that
    ///   validation runs against the schema and needs these five entries in
    ///   reach. <c>elementType</c> and <c>envVarOverridable</c> are read through
    ///   the indexer rather than <c>TryGetValue</c>, so both have to be present
    ///   whenever <c>envVar</c> is.
    /// </remarks>
    /// <param name="context">The value converters to serialise through.</param>
    /// <returns>The bag, or <c>null</c>.</returns>
    public Dictionary<string, object?>? ToExtensionData(UiEmitContext context)
    {
        var enumElement = Element as UiEnumElementBuilder;
        if (EnvironmentVariable is null && !RequiresRestart && enumElement is null)
            return null;

        var dict = new Dictionary<string, object?>
        {
            { "elementType", context.Convert(ElementType) },
            { "requiresRestart", RequiresRestart },
        };
        if (EnvironmentVariable is not null)
        {
            dict.Add("envVar", EnvironmentVariable);
            dict.Add("envVarOverridable", EnvironmentVariableOverridable);
        }
        if (enumElement is not null)
            dict.Add("enumDefinitions", enumElement.Values.Select(x => x.ToExtensionData()).ToList());
        return dict;
    }
}

/// <summary>
///   A user-invokable action attached to a container.
/// </summary>
internal sealed class UiActionBuilder
{
    /// <summary>The identifier sent back when the action is invoked.</summary>
    public required string ID { get; init; }

    /// <summary>The label of the action's button.</summary>
    public required string Title { get; init; }

    /// <summary>The description of the action.</summary>
    public required string Description { get; init; }

    /// <summary>The colour theme of the action's button.</summary>
    public required DisplayColorTheme Theme { get; init; }

    /// <summary>Where in the container the action's button belongs.</summary>
    public required DisplayButtonPosition Position { get; init; }

    /// <summary>The authored size of the action's button.</summary>
    public required DisplayElementSize Size { get; init; }

    /// <summary>The icon name for the action's button, if any.</summary>
    public string? Icon { get; init; }

    /// <summary>The member the action is attached to, if any.</summary>
    public string? MemberName { get; init; }

    /// <summary>The section the action belongs to, if any.</summary>
    public string? SectionName { get; init; }

    /// <summary>The condition controlling whether the action is shown.</summary>
    public UiConditionBuilder? Toggle { get; init; }

    /// <summary>The condition controlling whether the action is disabled.</summary>
    public UiConditionBuilder? Disable { get; init; }

    /// <summary>Whether the action is disabled while nothing changed.</summary>
    public required bool DisableIfNoChanges { get; init; }
}

/// <summary>
///   A lifecycle hook the configuration declared. Never emitted into the
///   schema; only used to work out which capabilities the configuration has.
/// </summary>
internal sealed class UiReactiveActionBuilder
{
    /// <summary>The name of the method behind the hook.</summary>
    public required string ID { get; init; }

    /// <summary>What the hook does.</summary>
    public required ConfigurationActionType ActionType { get; init; }

    /// <summary>Which reactive events the hook handles.</summary>
    public required ReactiveEventType EventType { get; init; }
}

/// <summary>
///   Everything the generator learned about a single class.
/// </summary>
internal sealed class UiClassBuilder
{
    private readonly Dictionary<string, UiPropertyBuilder> _propertiesByKey = new(StringComparer.Ordinal);

    private readonly List<UiPropertyBuilder> _properties = [];

    /// <summary>The class itself.</summary>
    public required Type Type { get; init; }

    /// <summary>
    ///   The definition for the class this one derives from, or <c>null</c> at
    ///   the top of the hierarchy.
    /// </summary>
    /// <remarks>
    ///   NJsonSchema hands a schema processor an inherited property through the
    ///   type that <em>declares</em> it, not through the type being generated,
    ///   so a derived class's own definition never sees the base's properties.
    ///   The schema flattens the hierarchy, so the lookups below have to
    ///   flatten it too.
    /// </remarks>
    public UiClassBuilder? BaseClass { get; set; }

    /// <summary>Whether the definition has already been populated.</summary>
    public bool IsPopulated { get; set; }

    /// <summary>The label of the container.</summary>
    public string? Label { get; set; }

    /// <summary>How the sections should be laid out.</summary>
    public DisplaySectionType SectionType { get; set; }

    /// <summary>The name of the container's default section, if authored.</summary>
    public string? SectionName { get; set; }

    /// <summary>
    ///   Whether floating sections go last, or <c>null</c> when the class
    ///   carries no <c>SectionAttribute</c> to say either way.
    /// </summary>
    public bool? AppendFloatingSectionsAtEnd { get; set; }

    /// <summary>The JSON name of the property that identifies an instance.</summary>
    public string? PrimaryKey { get; set; }

    /// <summary>The authored member order, actions included.</summary>
    public List<UiMemberOrderEntry> Structure { get; } = [];

    /// <summary>Whether the class opted out of the built-in save action.</summary>
    public bool HideSaveAction { get; set; }

    /// <summary>Whether the class opted into the built-in save action.</summary>
    public bool ShowSaveAction { get; set; }

    /// <summary>The actions attached to the container, in authored order.</summary>
    public List<UiActionBuilder> Actions { get; } = [];

    /// <summary>The lifecycle hooks the class declared.</summary>
    public List<UiReactiveActionBuilder> ReactiveActions { get; } = [];

    /// <summary>
    ///   The class's own property definitions, in the order they were seen,
    ///   followed by the ones it inherits.
    /// </summary>
    public IEnumerable<UiPropertyBuilder> Properties
        => BaseClass is null ? _properties : _properties.Concat(BaseClass.Properties);

    /// <summary>
    ///   Looks a property definition up by the key the generator filed it
    ///   under, falling back to the classes this one derives from.
    /// </summary>
    public UiPropertyBuilder? GetProperty(string key)
        => _propertiesByKey.GetValueOrDefault(key) ?? BaseClass?.GetProperty(key);

    /// <summary>
    ///   Returns the definition filed under <paramref name="key"/>, adding
    ///   <paramref name="builder"/> under it first if there is none.
    /// </summary>
    public UiPropertyBuilder GetOrAddProperty(string key, Func<UiPropertyBuilder> builder)
    {
        if (_propertiesByKey.TryGetValue(key, out var existing))
            return existing;

        var created = builder();
        _propertiesByKey.Add(key, created);
        _properties.Add(created);
        return created;
    }
}
