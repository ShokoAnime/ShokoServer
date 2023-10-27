namespace Shoko.Server.Filters.Interfaces;

public interface IFilterExpression
{
    bool TimeDependent { get; }
    bool UserDependent { get; }
    string HelpDescription { get; }
}

public interface IFilterExpression<out T>
{
    T Evaluate(IFilterable filterable, IFilterableUserInfo userInfo);
}
