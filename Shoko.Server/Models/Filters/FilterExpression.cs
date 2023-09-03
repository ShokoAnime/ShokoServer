using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public abstract class FilterExpression
{
    public int FilterExpressionID { get; set; }
    public string Type { get; set; }
    public abstract bool TimeDependent { get; }
    public abstract bool UserDependent { get; }
}

public abstract class FilterExpression<T> : FilterExpression, IFilterExpression<T>
{
    public abstract T Evaluate(IFilterable f);
}
