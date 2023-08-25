using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

public class HasTvDBLinkExpression : FilterExpression
{
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.HasTvDBLink;
}
