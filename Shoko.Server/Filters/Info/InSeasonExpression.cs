using System;
using Shoko.Models.Enums;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class InSeasonExpression : FilterExpression<bool>, IWithNumberParameter, IWithSecondStringParameter
{
    public InSeasonExpression(int year, AnimeSeason season)
    {
        Year = year;
        Season = season;
    }
    public InSeasonExpression() { }

    public int Year { get; set; }
    public AnimeSeason Season { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    
    double? IWithNumberParameter.Parameter
    {
        get => Year;
        set => Year = value.HasValue ? (int)value.Value : 0;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Season.ToString();
        set => Season = Enum.Parse<AnimeSeason>(value);
    }

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.Seasons.Contains((Year, Season));
    }

    protected bool Equals(InSeasonExpression other)
    {
        return base.Equals(other) && Year == other.Year && Season == other.Season;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((InSeasonExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Year, (int)Season);
    }

    public static bool operator ==(InSeasonExpression left, InSeasonExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(InSeasonExpression left, InSeasonExpression right)
    {
        return !Equals(left, right);
    }
}
