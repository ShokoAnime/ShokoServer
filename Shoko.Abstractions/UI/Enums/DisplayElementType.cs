using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// The type of element in the UI.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplayElementType
{
    /// <summary>
    /// The element type is automatically determined in the UI based on it's
    /// schema.
    /// </summary>
    [EnumMember(Value = "auto")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("auto")]
    Auto = 0,

    /// <summary>
    /// A container element holding a group of sections in the UI.
    /// </summary>
    [EnumMember(Value = "section-container")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("section-container")]
    SectionContainer = 1,

    /// <summary>
    /// A list element in the UI.
    /// </summary>
    [EnumMember(Value = "list")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("list")]
    List = 2,

    /// <summary>
    /// A record element in the UI.
    /// </summary>
    [EnumMember(Value = "record")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("record")]
    Record = 3,

    /// <summary>
    /// An enum element in the UI.
    /// </summary>
    [EnumMember(Value = "enum")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("enum")]
    Enum = 4,

    /// <summary>
    /// A password element in the UI.
    /// </summary>
    [EnumMember(Value = "password")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("password")]
    Password = 5,

    /// <summary>
    /// A text area element in the UI.
    /// </summary>
    [EnumMember(Value = "text-area")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("text-area")]
    TextArea = 6,

    /// <summary>
    /// A code block element in the UI.
    /// </summary>
    [EnumMember(Value = "code-block")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("code-block")]
    CodeBlock = 7,

    /// <summary>
    /// A select element in the UI.
    /// </summary>
    [EnumMember(Value = "select")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("select")]
    Select = 8,
}
