using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class InYearExpression : FilterExpression<bool>
{
    public int Parameter { get; set; }
    public override bool TimeDependent => true;
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.Years.Contains(Parameter);
}
