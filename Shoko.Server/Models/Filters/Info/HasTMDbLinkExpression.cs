using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

public class HasTMDbLinkExpression : FilterExpression
{
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.HasTMDbLink;
}
