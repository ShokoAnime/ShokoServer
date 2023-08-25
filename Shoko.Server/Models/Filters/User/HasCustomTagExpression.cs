namespace Shoko.Server.Models.Filters.User;

public class HasCustomTagExpression : FilterExpression
{
    public string Parameter { get; set; }
    public override bool UserDependent => true;
    public override bool Evaluate(IFilterable filterable) => filterable.CustomTags.Contains(Parameter);
}
