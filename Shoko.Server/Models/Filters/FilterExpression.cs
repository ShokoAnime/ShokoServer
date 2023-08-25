namespace Shoko.Server.Models.Filters;

public abstract class FilterExpression
{
    public abstract bool UserDependent { get; }

    public abstract bool Evaluate(IFilterable filterable);
}
