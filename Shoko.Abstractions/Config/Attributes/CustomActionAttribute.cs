using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Defines a custom action for a section in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CustomActionAttribute : Attribute
{
    private bool _hasToggleWhenSetTo;

    private bool _hasDisableWhenSetTo;

    private object? _disableWhenSetTo;

    private object? _toggleWhenSetTo;

    /// <summary>
    /// The MDI icon name in PascalName notation without the mdi- prefix.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// If the class has multiple floating sections assigned to it, this is the
    /// name of the section to place the action in. If the class doesn't have
    /// any floating sections, this will be ignored. And if it's not set and
    /// the class has any floating sections then it will be placed outside the
    /// sections at the given <see cref="Position"/>.
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// Determines the color theme of the action in the UI.
    /// </summary>
    public DisplayColorTheme Theme { get; set; }

    /// <summary>
    /// Determines the size of the action in the UI.
    /// </summary>
    public DisplayElementSize Size { get; set; }

    /// <summary>
    /// Gets or sets the position of the action within it's section in the UI.
    /// </summary>
    public DisplayButtonPosition Position { get; set; }

    /// <summary>
    /// When set, will attach the action to the specified member.
    /// </summary>
    public string? AttachToMember { get; set; }

    /// <summary>
    /// When set, will toggle the member from visible to hidden and vice versa. <seealso cref="ToggleWhenSetTo"/> also
    /// needs to be set for this to take effect. <seealso cref="InverseToggleCondition"/> will flip the functionality
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
    public bool HasToggleCondition => !string.IsNullOrEmpty(ToggleWhenMemberIsSet) && _hasToggleWhenSetTo;

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
    /// When set, will disable the action if no changes are made to the configuration.
    /// </summary>
    public bool DisableIfNoChanges { get; set; }
}
