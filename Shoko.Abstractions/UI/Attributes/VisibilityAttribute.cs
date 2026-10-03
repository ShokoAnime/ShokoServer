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
    /// The member whose value switches the visibility to
    /// <see cref="ToggleVisibilityTo"/>. <see cref="ToggleOperator"/> picks how
    /// it is compared.
    /// </summary>
    public string? ToggleWhenMemberIsSet { get; set; }

    /// <summary>
    /// Whether a value was authored for the condition. <c>null</c> is a value,
    /// so this is not the same as <see cref="ToggleWhenSetTo"/> being unset.
    /// </summary>
    public bool HasToggleValue => _hasToggleWhenSetTo;

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
    /// Whether the toggle condition names a member, has whatever its operator
    /// compares with, and switches to a visibility other than
    /// <see cref="Visibility"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ToggleWhenMemberIsSet))]
    [MemberNotNullWhen(true, nameof(_toggledVisibility))]
    public bool HasToggleCondition => !string.IsNullOrEmpty(ToggleWhenMemberIsSet) &&
        ConditionAuthoring.HasComparand(ToggleOperator, _hasToggleWhenSetTo, ToggleWhenSetToAny) &&
        _toggledVisibility.HasValue && _toggledVisibility.Value != Visibility;

    /// <summary>
    /// How <see cref="ToggleWhenMemberIsSet"/> is compared. Defaults to
    /// equality, and decides which of <see cref="ToggleWhenSetTo"/> and
    /// <see cref="ToggleWhenSetToAny"/> the condition reads, or neither for
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
    /// The member whose value disables the property/field.
    /// <see cref="DisableOperator"/> picks how it is compared.
    /// </summary>
    public string? DisableWhenMemberIsSet { get; set; }

    /// <summary>
    /// Whether a value was authored for the condition. <c>null</c> is a value,
    /// so this is not the same as <see cref="DisableWhenSetTo"/> being unset.
    /// </summary>
    public bool HasDisableValue => _hasDisableWhenSetTo;

    /// <summary>
    /// The value <see cref="DisableWhenMemberIsSet"/> is compared against, for
    /// an operator that compares one value.
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
    /// Whether the disable condition names a member and has whatever its
    /// operator compares with.
    /// </summary>
    [MemberNotNullWhen(true, nameof(DisableWhenMemberIsSet))]
    public bool HasDisableCondition => !string.IsNullOrEmpty(DisableWhenMemberIsSet) &&
        ConditionAuthoring.HasComparand(DisableOperator, _hasDisableWhenSetTo, DisableWhenSetToAny);

    /// <summary>
    /// How <see cref="DisableWhenMemberIsSet"/> is compared. Defaults to
    /// equality, and decides which of <see cref="DisableWhenSetTo"/> and
    /// <see cref="DisableWhenSetToAny"/> the condition reads, or neither for
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
