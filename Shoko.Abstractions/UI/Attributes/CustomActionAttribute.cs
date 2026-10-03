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
    /// The member whose value shows the action, which stays hidden otherwise.
    /// <see cref="ToggleOperator"/> picks how it is compared.
    /// </summary>
    public string? ToggleWhenMemberIsSet { get; set; }

    /// <summary>
    /// Whether a value was authored for the condition. <c>null</c> is a value,
    /// so this is not the same as <see cref="ToggleWhenSetTo"/> being unset.
    /// </summary>
    public bool HasToggleValue => _hasToggleWhenSetTo;

    /// <summary>
    /// The value <see cref="ToggleWhenMemberIsSet"/> is compared against, for
    /// an operator that compares one value.
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
    /// Whether the toggle condition names a member and has whatever its
    /// operator compares with.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ToggleWhenMemberIsSet))]
    public bool HasToggleCondition => !string.IsNullOrEmpty(ToggleWhenMemberIsSet) &&
        ConditionAuthoring.HasComparand(ToggleOperator, _hasToggleWhenSetTo, ToggleWhenSetToAny);

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
    /// The member whose value disables the action.
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
