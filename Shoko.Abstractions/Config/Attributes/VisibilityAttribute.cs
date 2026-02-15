using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Controls the visibility of a property/field in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class VisibilityAttribute : Attribute
{
    private bool _hasToggleWhenSetTo;

    private bool _hasDisableWhenSetTo;

    private object? _disableWhenSetTo;

    private object? _toggleWhenSetTo;

    private DisplayVisibility? _toggledVisibility;

    /// <summary>
    /// Gets or sets the default visibility of the property/field.
    /// </summary>
    public DisplayVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets the size of the property/field in the UI.
    /// </summary>
    public DisplayElementSize Size { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property/field is an
    /// advanced setting, and should be hidden from the UI until the user has
    /// enabled advanced mode.
    /// </summary>
    public bool Advanced { get; set; }

    /// <summary>
    /// Indicates that the visibility should change to <see cref="ToggleVisibilityTo"/> when the specified member is set to <see cref="ToggleVisibilityTo"/>.
    /// </summary>
    public string? ToggleWhenMemberIsSet { get; set; }

    /// <summary>
    /// Indicates that the visibility should change to <see cref="ToggleVisibilityTo"/> when <see cref="ToggleWhenMemberIsSet"/> is set to this value.
    /// </summary>
    public object? ToggleWhenSetTo
    {
        get => _toggleWhenSetTo;
        set
        {
            _toggleWhenSetTo = value;
            _hasToggleWhenSetTo = true;
        }
    }

    /// <summary>
    /// The visibility of the property/field when the toggle trigger is activated.
    /// </summary>
    public DisplayVisibility ToggleVisibilityTo
    {
        get => _toggledVisibility ?? Visibility;
        set => _toggledVisibility = value;
    }

    /// <summary>
    /// Indicates that the <see cref="ToggleVisibilityTo"/> property and the
    /// <see cref="ToggleWhenMemberIsSet"/> property is properly set
    /// and the toggle trigger should be toggled.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ToggleWhenMemberIsSet))]
    [MemberNotNullWhen(true, nameof(_toggledVisibility))]
    public bool HasToggleCondition => !string.IsNullOrEmpty(ToggleWhenMemberIsSet) && _hasToggleWhenSetTo && _toggledVisibility.HasValue && _toggledVisibility.Value != Visibility;

    /// <summary>
    /// Indicates that the action should be hidden by default. This means that <see cref="ToggleWhenMemberIsSet"/> and
    /// <see cref="ToggleWhenSetTo"/> will show the action instead of hiding it.
    /// </summary>
    public bool InverseToggleCondition { get; set; }

    /// <summary>
    /// When set, will toggle the member from visible to hidden and vice versa. <seealso cref="DisableWhenSetTo"/> also
    /// needs to be set for this to take effect. <seealso cref="InverseDisableCondition"/> will flip the functionality
    /// so it will toggle from hidden to visible instead of visible to hidden.
    /// </summary>
    public string? DisableWhenMemberIsSet { get; set; }

    /// <summary>
    /// Indicates that the visibility should change when the specified member is set to the specified value.
    /// </summary>
    public object? DisableWhenSetTo
    {
        get => _disableWhenSetTo;
        set
        {
            _disableWhenSetTo = value;
            _hasDisableWhenSetTo = true;
        }
    }

    /// <summary>
    /// Indicates that the <see cref="DisableWhenMemberIsSet"/> property is properly set
    /// and the toggle trigger should be toggled.
    /// </summary>
    [MemberNotNullWhen(true, nameof(DisableWhenMemberIsSet))]
    public bool HasDisableCondition => !string.IsNullOrEmpty(DisableWhenMemberIsSet) && _hasDisableWhenSetTo;

    /// <summary>
    /// Indicates that the action should be hidden by default. This means that <see cref="DisableWhenMemberIsSet"/> and
    /// <see cref="DisableWhenSetTo"/> will show the action instead of hiding it.
    /// </summary>
    public bool InverseDisableCondition { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VisibilityAttribute"/> class.
    /// </summary>
    public VisibilityAttribute() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="VisibilityAttribute"/> class.
    /// </summary>
    /// <param name="visibility">The visibility of the property/field.</param>
    public VisibilityAttribute(DisplayVisibility visibility) => Visibility = visibility;
}
