using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic;

public class NotExpression : FilterExpression<bool>
{
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override bool Evaluate(IFilterable filterable) => !Left.Evaluate(filterable);

    public FilterExpression<bool> Left { get; set; }
}
