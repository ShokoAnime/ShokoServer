using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// The type of element in the UI.
/// </summary>
public enum DisplayElementType
{
    /// <summary>
    /// The element type is automatically determined in the UI based on it's
    /// schema.
    /// </summary>
    [EnumMember(Value = "auto")]
    Auto = 0,

    /// <summary>
    /// A container element holding a group of sections in the UI.
    /// </summary>
    [EnumMember(Value = "section-container")]
    SectionContainer = 1,

    /// <summary>
    /// A list element in the UI.
    /// </summary>
    [EnumMember(Value = "list")]
    List = 2,

    /// <summary>
    /// A record element in the UI.
    /// </summary>
    [EnumMember(Value = "record")]
    Record = 3,

    /// <summary>
    /// An enum element in the UI.
    /// </summary>
    [EnumMember(Value = "enum")]
    Enum = 4,

    /// <summary>
    /// A password element in the UI.
    /// </summary>
    [EnumMember(Value = "password")]
    Password = 5,

    /// <summary>
    /// A text area element in the UI.
    /// </summary>
    [EnumMember(Value = "text-area")]
    TextArea = 6,

    /// <summary>
    /// A code block element in the UI.
    /// </summary>
    [EnumMember(Value = "code-block")]
    CodeBlock = 7,

    /// <summary>
    /// A select element in the UI.
    /// </summary>
    [EnumMember(Value = "select")]
    Select = 8,
}
