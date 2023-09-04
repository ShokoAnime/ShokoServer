using Shoko.Models.Enums;

namespace Shoko.Server.Filters.Info;

public class InSeasonExpression : FilterExpression<bool>
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

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.Seasons.Contains((Year, Season));
    }
}
