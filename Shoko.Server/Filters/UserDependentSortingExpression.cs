using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public abstract class UserDependentSortingExpression : SortingExpression, IUserDependentSortingExpression
{
    public override object Evaluate(IFilterable f)
    {
        if (UserDependent && f is not IUserDependentFilterable)
        {
            throw new ArgumentException("User Dependent Filter was given an Filterable, rather than an UserDependentFilterable");
        }

        return Evaluate((IUserDependentFilterable)f);
    }

    public abstract object Evaluate(IUserDependentFilterable f);
}
