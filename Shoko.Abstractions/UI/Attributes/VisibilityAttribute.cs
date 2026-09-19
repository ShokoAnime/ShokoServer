using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Attributes;

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
    /// Whether a value was authored for the condition. <c>null</c> is a value,
    /// so this is not the same as <see cref="ToggleWhenSetTo"/> being unset.
    /// </summary>
    public bool HasToggleValue => _hasToggleWhenSetTo;

    /// <summary>
    /// Whether the condition has whatever its operator needs to compare with.
    /// </summary>
    private bool HasToggleComparand => ToggleOperator is UiConditionOperator.IsEmpty or UiConditionOperator.IsNotEmpty ||
        (ToggleOperator is UiConditionOperator.In or UiConditionOperator.NotIn ? ToggleWhenSetToAny is { Length: > 0 } : _hasToggleWhenSetTo);

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
    public bool HasToggleCondition => !string.IsNullOrEmpty(ToggleWhenMemberIsSet) && HasToggleComparand && _toggledVisibility.HasValue && _toggledVisibility.Value != Visibility;

    /// <summary>
    /// How <see cref="ToggleWhenMemberIsSet"/> is compared. Defaults to
    /// equality, and decides which of <see cref="ToggleWhenSetTo"/> and
    /// <see cref="ToggleWhenSetToAny"/> the condition reads — or neither, for
    /// <see cref="UiConditionOperator.IsEmpty"/> and
    /// <see cref="UiConditionOperator.IsNotEmpty"/>.
    /// </summary>
    public UiConditionOperator ToggleOperator { get; set; }

    /// <summary>
    /// The values <see cref="ToggleWhenMemberIsSet"/> is compared against, for
    /// <see cref="UiConditionOperator.In"/> and
    /// <see cref="UiConditionOperator.NotIn"/>.
    /// </summary>
    public object?[]? ToggleWhenSetToAny { get; set; }

    /// <summary>
    /// When set, will toggle the member from visible to hidden and vice versa. <seealso cref="DisableWhenSetTo"/> also
    /// needs to be set for this to take effect. <seealso cref="DisableOperator"/> picks how the
    /// value is compared.
    /// </summary>
    public string? DisableWhenMemberIsSet { get; set; }

    /// <summary>
    /// Whether a value was authored for the condition. <c>null</c> is a value,
    /// so this is not the same as <see cref="DisableWhenSetTo"/> being unset.
    /// </summary>
    public bool HasDisableValue => _hasDisableWhenSetTo;

    /// <summary>
    /// Whether the condition has whatever its operator needs to compare with.
    /// </summary>
    private bool HasDisableComparand => DisableOperator is UiConditionOperator.IsEmpty or UiConditionOperator.IsNotEmpty ||
        (DisableOperator is UiConditionOperator.In or UiConditionOperator.NotIn ? DisableWhenSetToAny is { Length: > 0 } : _hasDisableWhenSetTo);

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
    public bool HasDisableCondition => !string.IsNullOrEmpty(DisableWhenMemberIsSet) && HasDisableComparand;

    /// <summary>
    /// How <see cref="DisableWhenMemberIsSet"/> is compared. Defaults to
    /// equality, and decides which of <see cref="DisableWhenSetTo"/> and
    /// <see cref="DisableWhenSetToAny"/> the condition reads — or neither, for
    /// <see cref="UiConditionOperator.IsEmpty"/> and
    /// <see cref="UiConditionOperator.IsNotEmpty"/>.
    /// </summary>
    public UiConditionOperator DisableOperator { get; set; }

    /// <summary>
    /// The values <see cref="DisableWhenMemberIsSet"/> is compared against, for
    /// <see cref="UiConditionOperator.In"/> and
    /// <see cref="UiConditionOperator.NotIn"/>.
    /// </summary>
    public object?[]? DisableWhenSetToAny { get; set; }

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
