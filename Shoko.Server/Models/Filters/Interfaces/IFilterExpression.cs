namespace Shoko.Server.Models.Filters.Interfaces;

public interface IFilterExpression
{
    bool TimeDependent { get; }
    bool UserDependent { get; }
}

public interface IFilterExpression<out T>
{
    T Evaluate(IFilterable f);
}

public interface IUserDependentFilterExpression<out T>
{
    T Evaluate(IUserDependentFilterable f);
}
