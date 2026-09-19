using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Determines the size of an element in the UI.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplayElementSize
{
    /// <summary>
    /// The element will span it's default size in the UI.
    /// </summary>
    [EnumMember(Value = "normal")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("normal")]
    Normal = 0,

    /// <summary>
    /// The element will span less the default size in the UI.
    /// </summary>
    [EnumMember(Value = "small")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("small")]
    Small = 1,

    /// <summary>
    /// The element will span more the default size in the UI.
    /// </summary>
    [EnumMember(Value = "large")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("large")]
    Large = 2,

    /// <summary>
    /// The element will span full size of it's container in the UI.
    /// </summary>
    [EnumMember(Value = "full")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("full")]
    Full = 3,
}
