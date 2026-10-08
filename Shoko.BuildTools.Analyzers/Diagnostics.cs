using Microsoft.CodeAnalysis;

namespace Shoko.BuildTools.Analyzers;

/// <summary>
/// The diagnostics reported by <see cref="ConfigurationTypeAnalyzer"/>.
/// </summary>
/// <remarks>
/// Diagnostic IDs are stable and must never be reused for a different rule. New rules append a new
/// number and a matching row in <c>AnalyzerReleases.Unshipped.md</c>.
/// </remarks>
public static class Diagnostics
{
    /// <summary>
    /// The category all authoring rules are reported under, actions included. The ID and the category
    /// are the analyzer's contract with a consumer's severity configuration, and every rule is about
    /// the UI schema generator.
    /// </summary>
    public const string Category = "Shoko.Configuration";

    private const string HelpLinkPrefix = "https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools.Analyzers/README.md#";

    /// <summary>
    /// A property nests a collection directly inside another collection.
    /// </summary>
    public static readonly DiagnosticDescriptor NestedCollection = new(
        id: "SHOKO0001",
        title: "Property nests a collection inside a collection",
        messageFormat: "Property '{0}' has type '{1}', which nests a collection inside a collection. The UI schema generator keys property metadata by the property name plus a single '+List' or '+Dict' suffix, so it can only describe one level of nesting; the property will render as a flat {2} or fail schema generation outright. Wrap the inner collection in a class and use a collection of that class instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The UI schema generator appends one '+List' or '+Dict' suffix per property when it stores the UI metadata for a property. A collection directly inside another collection makes both levels claim the same key (list in list, dictionary in dictionary), or makes the intermediate level unreachable (list in dictionary, dictionary in list), so the outer level's metadata is silently dropped. A list of dictionaries additionally throws during schema generation. Introduce a class for the inner collection so each level gets its own schema.",
        helpLinkUri: HelpLinkPrefix + "shoko0001");

    /// <summary>
    /// A property uses a dictionary key type that cannot be written as a JSON property name.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusableDictionaryKey = new(
        id: "SHOKO0002",
        title: "Dictionary key is not serializable to text",
        messageFormat: "Type '{1}' is not serializable to text and therefore cannot be used as a key in a dictionary the UI schema generator walks, but property '{0}' uses it as one. Schema generation throws, leaving the whole type without a schema and without a UI. Use 'string', an enum without [Flags], a type marked with [Serializable], or a type implementing ISerializable.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "JSON object keys are text. A [Flags] enum is written as a list of its members, so it is no key. The UI schema generator rejects any dictionary key type that is not a string, not an enum, not marked with [Serializable] (or, on the System.Text.Json path, [JsonSerializable]) and does not implement ISerializable, by throwing while building the schema. A key type coming from a reference assembly is not reported, because reference assemblies drop the [Serializable] metadata flag and the analyzer cannot tell.",
        helpLinkUri: HelpLinkPrefix + "shoko0002");

    /// <summary>
    /// A property's <c>[List]</c> display type does not match its element type.
    /// </summary>
    public static readonly DiagnosticDescriptor IncompatibleListType = new(
        id: "SHOKO0003",
        title: "List display type is not supported for these list items",
        messageFormat: "{1} lists are not supported for non-{2} list items, but property '{0}' sets ListType to '{3}' over elements of type '{4}'. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The complex list display types render one entry per class instance, so their elements have to be a class the generator renders as a section container: a type with at least one serialized property, declared outside the System and Shoko.Abstractions.UI.Components namespaces. The checkbox list display type renders one checkbox per enum member, so its elements have to be an enum.",
        helpLinkUri: HelpLinkPrefix + "shoko0003");

    /// <summary>
    /// A complex <c>[List]</c> display type is used without anything to key the entries by.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingPrimaryKey = new(
        id: "SHOKO0004",
        title: "Complex list display type has no primary key",
        messageFormat: "{1} lists must have a primary key set, but neither property '{0}' nor its item type '{2}' declares a [Key] property. Schema generation throws, leaving the whole type without a schema and without a UI. Put [Key] on a property declared by '{2}' itself, or on '{0}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A complex list needs to label each entry, which it takes from a property annotated with [Key]. The generator files a property's UI metadata under its reflected type, so a [Key] inherited from a base class never reaches the derived item type's primary key scan and does not count; the same goes for a [Key] on a property either JSON serializer ignores.",
        helpLinkUri: HelpLinkPrefix + "shoko0004");

    /// <summary>
    /// A property is rendered as a record but is not a generic dictionary.
    /// </summary>
    public static readonly DiagnosticDescriptor NotAGenericDictionary = new(
        id: "SHOKO0005",
        title: "Record-shaped property is not a generic dictionary",
        messageFormat: "Type '{1}' does not implement IReadOnlyDictionary<,> or IDictionary<,>, but property '{0}' has that type and the schema generator renders it as a record. Schema generation throws, leaving the whole type without a schema and without a UI. Use 'Dictionary<TKey, TValue>' or another generic dictionary.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A non-generic dictionary such as Hashtable still becomes a JSON object with additionalProperties, so the generator takes the record path and then asks the type for its key and value types. Only the generic dictionary interfaces can answer that.",
        helpLinkUri: HelpLinkPrefix + "shoko0005");

    /// <summary>
    /// A condition compares something it cannot compare, or names something that is not there.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusableCondition = new(
        id: "SHOKO0006",
        title: "Condition can never hold",
        messageFormat: "The condition on '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A condition names a member of the type it is declared on, optionally descending into a nested class, and compares it with an operator. The operator decides what it is handed: the emptiness operators take no value, the set operators take a value set, and every other operator takes a single value. An ordering comparison needs numbers on both sides, and a containment check needs a string or a collection. A path cannot point through a list or a dictionary, because it carries no index to say which entry it meant.",
        helpLinkUri: HelpLinkPrefix + "shoko0006");

    /// <summary>
    /// A lifecycle hook narrows what it reacts to, and cannot.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusableReactiveHandler = new(
        id: "SHOKO0007",
        title: "Handler cannot react to what it names",
        messageFormat: "The handler '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A live-edit handler names the members it watches so a client can tell whether an edit is worth sending, and the events it wants so the client knows when to send one. A member the class does not have is watched by nothing, and a name pointing through a list or a dictionary carries no index to say which entry it meant. Only the live-edit hook is raised by an event, so no other hook can narrow what it reacts to.",
        helpLinkUri: HelpLinkPrefix + "shoko0007");

    /// <summary>
    /// An options provider method cannot be called, or returns no collection.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusableOptionsProviderMethod = new(
        id: "SHOKO0008",
        title: "Options provider method cannot be used",
        messageFormat: "The options provider '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only a method carrying [OptionsProvider] is checked, and any other method is the class's own. That method is public and not generic, and returns a collection of options, an array or any IEnumerable, directly or through a Task or ValueTask.",
        helpLinkUri: HelpLinkPrefix + "shoko0008");

    /// <summary>
    /// An options provider names no member, a member the class does not have, or a member twice.
    /// </summary>
    public static readonly DiagnosticDescriptor UnknownOptionsMember = new(
        id: "SHOKO0009",
        title: "Options provider names no usable member",
        messageFormat: "The options provider '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[OptionsProvider] names at least one member, and each one once. Every name is a public instance property of the class declaring the method, as nameof gives it.",
        helpLinkUri: HelpLinkPrefix + "shoko0009");

    /// <summary>
    /// A named member has no part the options could be for.
    /// </summary>
    public static readonly DiagnosticDescriptor MemberTakesNoOptions = new(
        id: "SHOKO0010",
        title: "Member cannot take options",
        messageFormat: "The options provider '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Values, the default target, is a scalar itself, a list's entries or a dictionary's values; Keys is a dictionary's keys. A select component carries its own options, and a dictionary of dictionaries or one without key and value types has no single value to offer options for.",
        helpLinkUri: HelpLinkPrefix + "shoko0010");

    /// <summary>
    /// A named member's options would be of a type that cannot be told apart as text.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusableOptionType = new(
        id: "SHOKO0011",
        title: "Option type is not allowed",
        messageFormat: "The options provider '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An option is a primitive, a string, a decimal, an enum, a Guid, a date, time or time span, a Uri, or a type of any origin that implements IParsable of itself or carries [TypeConverter], with nullable types unwrapped. The startup check also makes sure the converter turns the type into text or a primitive and back.",
        helpLinkUri: HelpLinkPrefix + "shoko0011");

    /// <summary>
    /// The named members take different options, or the method lists another type.
    /// </summary>
    public static readonly DiagnosticDescriptor OptionTypeMismatch = new(
        id: "SHOKO0012",
        title: "Options do not match their members",
        messageFormat: "The options provider '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every member one provider names takes the same option type, and the method lists exactly that type, or SelectOption of it to label each option. A conversion decides what an option may be, never which member it fits, so no type stands in for another.",
        helpLinkUri: HelpLinkPrefix + "shoko0012");

    /// <summary>
    /// A part of a member already has a provider.
    /// </summary>
    public static readonly DiagnosticDescriptor OptionsClaimedTwice = new(
        id: "SHOKO0013",
        title: "Member's options are provided twice",
        messageFormat: "The options provider '{0}' {1}. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each part of a member has one provider at most. A dictionary's keys and values are separate parts, so each may have its own.",
        helpLinkUri: HelpLinkPrefix + "shoko0013");

    /// <summary>
    /// A <c>[Flags]</c> enum has no single-bit member for its list to hold.
    /// </summary>
    public static readonly DiagnosticDescriptor FlagEnumWithoutMembers = new(
        id: "SHOKO0014",
        title: "Flags enum has no single-bit members",
        messageFormat: "Property '{0}' holds the flags enum '{1}', which has no member with a single bit set. A flags enum is rendered as a list of its single-bit members, so this one has nothing to offer. Schema generation throws, leaving the whole type without a schema and without a UI.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Flags] enum is described as a list of its members with a single bit set. Zero and members combining several bits are only accepted on read. An enum with no single-bit member has no entries to list, so give it one per flag, or drop [Flags].",
        helpLinkUri: HelpLinkPrefix + "shoko0014");
}
