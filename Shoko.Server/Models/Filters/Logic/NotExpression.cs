namespace Shoko.Server.Models.Filters.Logic;

public class NotExpression : FilterExpression
{
    public override bool UserDependent => Left.UserDependent;
    public override bool Evaluate(IFilterable filterable) => !Left.Evaluate(filterable);

    public FilterExpression Left { get; set; }
}
