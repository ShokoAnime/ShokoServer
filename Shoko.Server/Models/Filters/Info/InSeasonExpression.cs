using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

/// <summary>
/// Parameter is a season followed by year, ie Winter 2022
/// </summary>
public class InSeasonExpression : FilterExpression
{
    public string Parameter { get; set; }
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.Seasons.Contains(Parameter);
}
