using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Attributes;

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
    /// The name of the section to place the action in, the same name a
    /// <see cref="SectionNameAttribute"/> on a property gives. When it's not
    /// set, the action stays where it was authored among the class's own
    /// members, unless the class gathers its unsectioned members into a default
    /// section, in which case the action joins them there.
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
    /// When set, the action renders on the row of the named member rather than
    /// among the class's own members, with <see cref="Position"/> choosing which
    /// edge of that row it sits on.
    /// </summary>
    /// <remarks>
    /// The name is the member's own, as <c>nameof</c> gives it, even where the
    /// serializer renames it in the document. An action naming a member the
    /// class does not have renders with the rest instead.
    /// </remarks>
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
    /// Indicates that the action should be disabled by default. This means that <see cref="DisableWhenMemberIsSet"/> and
    /// <see cref="DisableWhenSetTo"/> will show the action instead of hiding it.
    /// </summary>
    public bool InverseDisableCondition { get; set; }

    /// <summary>
    /// When set, will disable the action if no changes are made to the configuration.
    /// </summary>
    /// <remarks>
    /// Configuration-only: it compares the edited document against the saved
    /// one, and an executable action's parameter form has nothing saved to
    /// compare against. It stays on this otherwise shared attribute rather than
    /// splitting the attribute in two over a single property.
    /// </remarks>
    public bool DisableIfNoChanges { get; set; }
}
