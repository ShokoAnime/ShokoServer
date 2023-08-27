using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

public class IsFinishedExpression : FilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.IsFinished;
}
