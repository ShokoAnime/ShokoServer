using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

public class HasVideoSourceExpression : FilterExpression
{
    public string Parameter { get; set; }
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.VideoSources.Contains(Parameter);
}
