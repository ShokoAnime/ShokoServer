using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public abstract class UserDependentFilterExpression<T> : FilterExpression<T>, IUserDependentFilterExpression<T>
{

    public abstract T Evaluate(UserDependentFilterable f);

    public override T Evaluate(Filterable f)
    {
        if (UserDependent && f is not UserDependentFilterable)
        {
            throw new ArgumentException("User Dependent Filter was given an Filterable, rather than an UserDependentFilterable");
        }

        return Evaluate((UserDependentFilterable)f);
    }
}
