using Shoko.Models.Enums;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

public class InSeasonExpression : FilterExpression<bool>
{
    public int Year { get; set; }
    public AnimeSeason Season { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.Seasons.Contains((Year, Season));
}
