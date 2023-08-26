using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public abstract class FilterExpression<T> : IFilterExpression<T>
{
    public abstract bool TimeDependent { get; }
    public abstract bool UserDependent { get; }

    public abstract T Evaluate(IFilterable f);
}
