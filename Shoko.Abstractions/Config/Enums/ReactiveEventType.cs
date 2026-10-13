using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// Reason for a reactive event.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ReactiveEventType
{
    /// <summary>
    ///   No specific event.
    /// </summary>
    [EnumMember(Value = "all")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("all")]
    All = 0,

    /// <summary>
    ///   A field has been edited.
    /// </summary>
    [EnumMember(Value = "edited")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("edited")]
    Edited = 1,

    /// <summary>
    ///   A field has been focused.
    /// </summary>
    [EnumMember(Value = "focused")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("focused")]
    Focused = 2,

    /// <summary>
    ///   A field has been unfocused.
    /// </summary>
    [EnumMember(Value = "unfocused")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unfocused")]
    Unfocused = 3,

    /// <summary>
    ///   The view has changed.
    /// </summary>
    [EnumMember(Value = "view-changed")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("view-changed")]
    ViewChanged = 4,

    /// <summary>
    ///   A new value is being edited before being added.
    /// </summary>
    [EnumMember(Value = "new-value")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("new-value")]
    NewValue = 5,

    /// <summary>
    ///   The field has been clicked.
    /// </summary>
    [EnumMember(Value = "clicked")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("clicked")]
    Clicked = 6,
}
