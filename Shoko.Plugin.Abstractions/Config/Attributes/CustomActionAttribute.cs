using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Defines a custom action for a section in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CustomActionAttribute : Attribute
{
    private bool _hasToggleWhenSetTo;

    private object? _toggleWhenSetTo;

    /// <summary>
    /// Name of the custom action.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of the custom action.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Determines the color theme of the action in the UI.
    /// </summary>
    public DisplayColorTheme Theme { get; set; }

    /// <summary>
    /// Gets or sets the position of the action within it's section in the UI.
    /// </summary>
    public DisplayButtonPosition Position { get; set; }

    /// <summary>
    /// When set, will toggle the member from visible to hidden and vice versa. <seealso cref="ToggleWhenSetTo"/> also
    /// needs to be set for this to take effect. <seealso cref="HideByDefault"/> will flip the functionality
    /// so it will toggle from hidden to visible instead of visible to hidden.
    /// </summary>
    public string? ToggleWhenMemberIsSet { get; set; }

    /// <summary>
    /// Indicates that the visibility should change when the specified member is set to the specified value.
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
    /// Indicates that the <see cref="ToggleWhenMemberIsSet"/> property is properly set
    /// and the toggle trigger should be toggled.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ToggleWhenMemberIsSet))]
    public bool HasToggle => !string.IsNullOrEmpty(ToggleWhenMemberIsSet) && _hasToggleWhenSetTo;

    /// <summary>
    /// Indicates that the action should be hidden by default. This means that <see cref="ToggleWhenMemberIsSet"/> and
    /// <see cref="ToggleWhenSetTo"/> will show the action instead of hiding it.
    /// </summary>
    public bool HideByDefault { get; set; }

    /// <summary>
    /// When set, will disable the action if no changes are made to the configuration.
    /// </summary>
    public bool DisableIfNoChanges { get; set; }

    /// <summary>
    /// Creates a new instance of the <see cref="CustomActionAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    public CustomActionAttribute(string name)
    {
        Name = name;
    }
}
