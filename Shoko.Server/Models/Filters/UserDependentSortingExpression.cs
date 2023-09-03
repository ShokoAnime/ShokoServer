using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public abstract class UserDependentSortingExpression<T> : SortingExpression<T>, IUserDependentSortingExpression<T> where T : IComparable
{
    public override T Evaluate(IFilterable f)
    {
        if (UserDependent && f is not IUserDependentFilterable)
            throw new ArgumentException("User Dependent Filter was given an IFilterable, rather than an IUserDependentFilterable");

        return Evaluate((IUserDependentFilterable)f);
    }

    public abstract T Evaluate(IUserDependentFilterable f);
}
