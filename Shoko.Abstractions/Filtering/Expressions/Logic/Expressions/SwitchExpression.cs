using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// A switch around a condition. Switched on, it passes if the left expression
/// passes. Switched off, or with no left expression, the logic around it
/// leaves it out, as if it and its condition were not there; with nothing
/// around to leave it out, it fails. The label does not change what it matches.
/// </summary>
public class SwitchExpression : FilterExpression<bool>, IWithExpressionParameter, IWithBoolParameter, IWithSecondStringParameter
{
    /// <summary>
    /// Creates a switch around an expression.
    /// </summary>
    /// <param name="left">The condition the switch turns on and off, or <c>null</c> for none yet.</param>
    /// <param name="isOn">Whether the switch is on.</param>
    /// <param name="label">The switch's label, or <c>null</c> for none. An empty label is none.</param>
    public SwitchExpression(FilterExpression<bool>? left, bool isOn, string? label = null)
    {
        Left = left;
        IsOn = isOn;
        Label = label;
    }

    /// <summary>
    /// Creates a switched-off switch with no condition, for deserialization.
    /// </summary>
    public SwitchExpression() { }

    /// <summary>
    /// The condition the switch turns on and off, or <c>null</c> for none yet.
    /// A switch without one counts as switched off.
    /// </summary>
    public FilterExpression<bool>? Left { get; set; }

    /// <summary>
    /// Whether the switch is on. Kept as the parameter, so a switch whose
    /// parameter is not set is off.
    /// </summary>
    public bool IsOn { get; set; }

    /// <summary>
    /// The switch's label, or <c>null</c> for none. Kept as the second
    /// parameter; an empty label is none.
    /// </summary>
    public string? Label
    {
        get;
        set => field = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <inheritdoc/>
    public override bool TimeDependent => IsOn && (Left?.TimeDependent ?? false);

    /// <inheritdoc/>
    public override bool UserDependent => IsOn && (Left?.UserDependent ?? false);

    /// <inheritdoc/>
    public override bool IsApplicable => IsOn && Left is { IsApplicable: true };

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the switch is on and the left expression passes. Switched off, or with no left expression, the logic around it leaves it out";

    /// <inheritdoc/>
    public override string HelpParameterName => "On";

    /// <inheritdoc/>
    public override string HelpSecondParameterName => "Label";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    bool? IWithBoolParameter.Parameter
    {
        get => IsOn;
        set => IsOn = value ?? false;
    }

    string? IWithSecondStringParameter.SecondParameter
    {
        get => Label;
        set => Label = value;
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        if (!IsOn)
            return false;

        return Left?.Evaluate(filterable, userInfo, time) ?? false;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(SwitchExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && IsOn == other.IsOn && Label == other.Label;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        return Equals((SwitchExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, IsOn, Label);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is SwitchExpression exp && (Left?.IsType(exp.Left) ?? true);
}
