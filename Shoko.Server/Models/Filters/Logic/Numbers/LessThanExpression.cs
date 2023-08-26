using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.Numbers;

public class LessThanExpression : FilterExpression<bool>
{
    public FilterExpression<double> Selector { get; set; }
    public double Parameter { get; set; }
    public override bool TimeDependent => Selector.TimeDependent;
    public override bool UserDependent => Selector.UserDependent;
    public override bool Evaluate(IFilterable filterable) => Selector.Evaluate(filterable) < Parameter;
}
