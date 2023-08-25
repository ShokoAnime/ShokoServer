using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.Numbers;

public class GreaterThanEqualExpression : FilterExpression
{
    public INumberSelector Selector { get; set; }
    public double Parameter { get; set; }
    public override bool UserDependent => Selector.UserDependent;
    public override bool Evaluate(IFilterable filterable) => Selector.Selector(filterable) >= Parameter;
}
