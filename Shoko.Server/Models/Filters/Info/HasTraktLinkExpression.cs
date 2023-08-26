using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

public class HasTraktLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.HasTraktLink;
}
