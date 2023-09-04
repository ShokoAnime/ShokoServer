using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasCustomTagExpression : FilterExpression<bool>
{
    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override bool Evaluate(IFilterable filterable) => filterable.CustomTags.Contains(Parameter);
}
