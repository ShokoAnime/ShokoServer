namespace Shoko.Server.Models.Filters.Interfaces;

public interface IFilterExpression
{
    bool TimeDependent { get; }
    bool UserDependent { get; }
}

public interface IFilterExpression<out T> : IFilterExpression
{
    T Evaluate(IFilterable f);
}
