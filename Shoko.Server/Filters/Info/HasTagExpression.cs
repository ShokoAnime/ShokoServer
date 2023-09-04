using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasTagExpression : FilterExpression<bool>
{
    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.Tags.Contains(Parameter);
}
