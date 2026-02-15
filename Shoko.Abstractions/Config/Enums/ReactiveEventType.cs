using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// Reason for a reactive event.
/// </summary>
public enum ReactiveEventType
{
    /// <summary>
    ///   No specific event.
    /// </summary>
    [EnumMember(Value = "all")]
    All = 0,

    /// <summary>
    ///   A field has been edited.
    /// </summary>
    [EnumMember(Value = "edited")]
    Edited = 1,

    /// <summary>
    ///   A field has been focused.
    /// </summary>
    [EnumMember(Value = "focused")]
    Focused = 2,

    /// <summary>
    ///   A field has been unfocused.
    /// </summary>
    [EnumMember(Value = "unfocused")]
    Unfocused = 3,

    /// <summary>
    ///   The view has changed.
    /// </summary>
    [EnumMember(Value = "view-changed")]
    ViewChanged = 4,

    /// <summary>
    ///   A new value is being edited before being added.
    /// </summary>
    [EnumMember(Value = "new-value")]
    NewValue = 5,

    /// <summary>
    ///   The field has been clicked.
    /// </summary>
    [EnumMember(Value = "clicked")]
    Clicked = 6,
}
