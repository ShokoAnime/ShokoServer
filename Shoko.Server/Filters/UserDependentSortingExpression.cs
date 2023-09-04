using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public abstract class UserDependentSortingExpression : SortingExpression, IUserDependentSortingExpression
{
    public override object Evaluate(Filterable f)
    {
        if (UserDependent && f is not UserDependentFilterable)
        {
            throw new ArgumentException("User Dependent Filter was given an Filterable, rather than an UserDependentFilterable");
        }

        return Evaluate((UserDependentFilterable)f);
    }

    public abstract object Evaluate(UserDependentFilterable f);
}
