namespace Shoko.Server.Filters.Interfaces;

public interface IFilterExpression
{
    bool TimeDependent { get; }
    bool UserDependent { get; }
}

public interface IFilterExpression<out T>
{
    T Evaluate(Filterable f);
}

public interface IUserDependentFilterExpression<out T>
{
    T Evaluate(UserDependentFilterable f);
}
