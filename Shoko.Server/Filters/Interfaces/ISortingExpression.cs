namespace Shoko.Server.Filters.Interfaces;

public interface ISortingExpression : IFilterExpression<object> { }

public interface IUserDependentSortingExpression : ISortingExpression, IUserDependentFilterExpression<object> { }
