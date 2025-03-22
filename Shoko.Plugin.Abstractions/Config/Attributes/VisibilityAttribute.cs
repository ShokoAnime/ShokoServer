using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Controls the visibility of a property/field in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class VisibilityAttribute : Attribute
{
    private bool _hasToggleWhenSetTo;

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
    public bool HasToggle => !string.IsNullOrEmpty(ToggleWhenMemberIsSet) && _hasToggleWhenSetTo && _toggledVisibility.HasValue && _toggledVisibility.Value != Visibility;

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
