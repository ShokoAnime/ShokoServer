using System;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// A three-way switch around a condition. It keeps only, or leaves out, what
/// its condition matches; set to keep everything, or left unset, the logic
/// around it leaves it out, as if it and its condition were not there, and
/// with nothing around to leave it out it fails. The label does not change
/// what it matches.
/// </summary>
public class SwitchExpression : FilterExpression<bool>, IWithExpressionParameter, IWithStringParameter, IWithSecondStringParameter
{
    /// <summary>
    /// Creates a switch around an expression.
    /// </summary>
    /// <param name="left">The condition the switch acts on, or <c>null</c> for none yet.</param>
    /// <param name="inclusion">
    /// What the switch does with the condition: <see cref="InclusionFilter.Only"/> keeps only what it
    /// matches, <see cref="InclusionFilter.False"/> leaves out what it matches, and
    /// <see cref="InclusionFilter.True"/> or <c>null</c> leaves the switch out of the filter.
    /// </param>
    /// <param name="label">The switch's label, or <c>null</c> for none. An empty label is none.</param>
    public SwitchExpression(FilterExpression<bool>? left, InclusionFilter? inclusion, string? label = null)
    {
        Left = left;
        Inclusion = inclusion;
        Label = label;
    }

    /// <summary>
    /// Creates an unset switch with no condition, for deserialization.
    /// </summary>
    public SwitchExpression() { }

    /// <summary>
    /// The condition the switch acts on, or <c>null</c> for none yet. A switch
    /// without one is left out of the filter.
    /// </summary>
    public FilterExpression<bool>? Left { get; set; }

    /// <summary>
    /// The switch's label, or <c>null</c> for none. Kept as the parameter, like
    /// a group's label; an empty label is none.
    /// </summary>
    public string? Label
    {
        get;
        set => field = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// What the switch does with its condition, kept as the second parameter:
    /// <see cref="InclusionFilter.Only"/> keeps only what it matches,
    /// <see cref="InclusionFilter.False"/> leaves out what it matches, and
    /// <see cref="InclusionFilter.True"/> (keep everything) or <c>null</c>
    /// leaves the switch out of the filter.
    /// </summary>
    public InclusionFilter? Inclusion { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => IsApplicable && Left!.TimeDependent;

    /// <inheritdoc/>
    public override bool UserDependent => IsApplicable && Left!.UserDependent;

    /// <inheritdoc/>
    public override bool IsApplicable
        => Inclusion is InclusionFilter.Only or InclusionFilter.False && Left is { IsApplicable: true };

    /// <inheritdoc/>
    public override string HelpDescription => "This condition keeps only, or leaves out, what its condition matches. Set to keep everything, or left unset, it is left out of the filter";

    /// <inheritdoc/>
    public override string HelpParameterName => "Label";

    /// <inheritdoc/>
    public override string HelpSecondParameterName => "Inclusion";

    /// <summary>
    /// The values the inclusion takes, the names of <see cref="InclusionFilter"/>:
    /// <c>False</c> leaves out what the condition matches, <c>True</c> keeps
    /// everything, and <c>Only</c> keeps only what it matches.
    /// </summary>
    public override string[] HelpPossibleSecondParameters => [.. Enum.GetValues<InclusionFilter>().Select(value => value.ToString())];

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    string? IWithStringParameter.Parameter
    {
        get => Label;
        set => Label = value;
    }

    string? IWithSecondStringParameter.SecondParameter
    {
        get => Inclusion?.ToString();
        set => Inclusion = Enum.TryParse<InclusionFilter>(value, ignoreCase: true, out var inclusion) && Enum.IsDefined(inclusion) ? inclusion : null;
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        if (!IsApplicable)
            return false;

        var passes = Left!.Evaluate(filterable, userInfo, time);
        return Inclusion is InclusionFilter.Only ? passes : !passes;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(SwitchExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Inclusion == other.Inclusion && Label == other.Label;
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
        => HashCode.Combine(base.GetHashCode(), Left, Inclusion, Label);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is SwitchExpression exp && (Left?.IsType(exp.Left) ?? true);
}
