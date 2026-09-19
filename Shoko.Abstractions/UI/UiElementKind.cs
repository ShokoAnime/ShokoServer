using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI;

/// <summary>
/// Discriminator for the concrete <see cref="UiElement"/> subclass carried by a
/// node in a <see cref="UiDefinition"/>.
/// </summary>
/// <remarks>
/// Every value maps to exactly one renderer on the client. There is
/// deliberately no <c>auto</c> member; the server resolves the authored intent
/// down to a concrete element before the definition leaves the process.
/// </remarks>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum UiElementKind
{
    /// <summary>
    /// The server was unable to map the underlying schema node onto a known
    /// element. The client should render a placeholder.
    /// </summary>
    [EnumMember(Value = "unknown")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unknown")]
    Unknown = 0,

    /// <summary>
    /// A pointer into <see cref="UiDefinition.Definitions"/>, emitted where the
    /// element tree would otherwise recurse into itself.
    /// </summary>
    [EnumMember(Value = "reference")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("reference")]
    Reference = 1,

    /// <summary>
    /// A container holding an ordered list of child elements grouped into
    /// sections.
    /// </summary>
    [EnumMember(Value = "section-container")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("section-container")]
    SectionContainer = 2,

    /// <summary>
    /// A boolean toggle.
    /// </summary>
    [EnumMember(Value = "boolean")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("boolean")]
    Boolean = 3,

    /// <summary>
    /// A whole-number input.
    /// </summary>
    [EnumMember(Value = "integer")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("integer")]
    Integer = 4,

    /// <summary>
    /// A fractional-number input.
    /// </summary>
    [EnumMember(Value = "float")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("float")]
    Float = 5,

    /// <summary>
    /// A single-line text input.
    /// </summary>
    [EnumMember(Value = "string")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("string")]
    String = 6,

    /// <summary>
    /// A multi-line text input.
    /// </summary>
    [EnumMember(Value = "text-area")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("text-area")]
    TextArea = 7,

    /// <summary>
    /// A masked text input.
    /// </summary>
    [EnumMember(Value = "password")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("password")]
    Password = 8,

    /// <summary>
    /// A syntax-highlighted code editor.
    /// </summary>
    [EnumMember(Value = "code-editor")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("code-editor")]
    CodeEditor = 9,

    /// <summary>
    /// A choice between a fixed set of named values.
    /// </summary>
    [EnumMember(Value = "enum")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("enum")]
    Enum = 10,

    /// <summary>
    /// An ordered collection of items of a single element kind.
    /// </summary>
    [EnumMember(Value = "list")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("list")]
    List = 11,

    /// <summary>
    /// A keyed collection of values of a single element kind.
    /// </summary>
    [EnumMember(Value = "record")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("record")]
    Record = 12,

    /// <summary>
    /// A server-populated selection component.
    /// </summary>
    [EnumMember(Value = "select")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("select")]
    Select = 13,
}
