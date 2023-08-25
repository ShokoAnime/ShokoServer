using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

public class HasTraktLinkExpression : FilterExpression
{
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.HasTraktLink;
}
