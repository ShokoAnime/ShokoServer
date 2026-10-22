using System;
using Newtonsoft.Json;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Expressions.Converters;
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

    /// <summary>
    /// The year of the season, kept as the parameter, or <c>null</c> when it
    /// is not set.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// The season within the year, kept as the second parameter, or
    /// <c>null</c> when it is not set.
    /// </summary>
    [JsonConverter(typeof(LenientEnumNewtonsoftJsonConverter))]
    public YearlySeason? Season { get; set; }

    /// <inheritdoc/>
    public override bool IsApplicable => Year is not null && Season is not null;

    /// <inheritdoc/>
    public override bool TimeDependent => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime aired in the specified season and year";

    /// <inheritdoc/>
    public override string HelpParameterName => "Year";

    /// <inheritdoc/>
    public override string HelpSecondParameterName => "Season";

    double? IWithNumberParameter.Parameter
    {
        get => Year;
        set => Year = value is { } year ? (int)year : null;
    }

    string? IWithSecondStringParameter.SecondParameter
    {
        get => Season?.ToString();
        set => Season = value is null ? null : Enum.Parse<YearlySeason>(value, ignoreCase: true);
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Year is { } year && Season is { } season && filterable.Seasons.Contains((year, season));
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
        return HashCode.Combine(base.GetHashCode(), Year, Season);
    }
}
