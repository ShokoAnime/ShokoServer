using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Which part of a member an options provider lists values for.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum OptionsTarget
{
    /// <summary>
    /// The value: a scalar itself, a list's entries, or a dictionary's values.
    /// </summary>
    [EnumMember(Value = "values")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("values")]
    Values = 0,

    /// <summary>
    /// A dictionary's keys.
    /// </summary>
    [EnumMember(Value = "keys")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("keys")]
    Keys = 1,
}
