using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime aired in the specified season and year
/// </summary>
public class InSeasonExpression : FilterExpression<bool>, IWithNumberParameter, IWithSecondStringParameter
{
    /// <inheritdoc/>
    public InSeasonExpression(int year, YearlySeason season)
        => (Year, Season) = (year, season);

    /// <inheritdoc/>
    public InSeasonExpression() { }

    /// <inheritdoc/>
    protected int Year { get; set; }

    /// <inheritdoc/>
    protected YearlySeason Season { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime aired in the specified season and year";

    double IWithNumberParameter.Parameter
    {
        get => Year;
        set => Year = (int)value;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Season.ToString();
        set => Season = Enum.Parse<YearlySeason>(value, ignoreCase: true);
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.Seasons.Contains((Year, Season));
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(InSeasonExpression other)
    {
        return base.Equals(other) && Year == other.Year && Season == other.Season;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((InSeasonExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Year, (int)Season);
    }
}
