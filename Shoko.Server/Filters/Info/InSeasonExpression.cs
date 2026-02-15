using System;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters.Info;

public class InSeasonExpression : FilterExpression<bool>, IWithNumberParameter, IWithSecondStringParameter
{
    public InSeasonExpression(int year, YearlySeason season)
    {
        Year = year;
        Season = season;
    }
    public InSeasonExpression() { }

    public int Year { get; set; }
    public YearlySeason Season { get; set; }

    public override bool TimeDependent => true;
    public override string HelpDescription => "This condition passes if any of the anime aired in the specified season and year";
    public override string[][] HelpPossibleParameterPairs => RepoFactory.AnimeSeries.GetAllSeasons().Select(a => new[] { a.Year.ToString(), a.Season.ToString() }).ToArray();

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

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
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
