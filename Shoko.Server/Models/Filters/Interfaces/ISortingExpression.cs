namespace Shoko.Server.Models.Filters.Interfaces;

public interface ISortingExpression
{
    bool TimeDependent { get; }
    bool UserDependent { get; }
}

public interface ISortingExpression<T>
{
    T Evaluate(IFilterable f);
}

public interface IUserDependentSortingExpression<T>
{
    T Evaluate(IUserDependentFilterable f);
}
