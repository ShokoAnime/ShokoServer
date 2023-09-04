using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public abstract class UserDependentFilterExpression<T> : FilterExpression<T>, IUserDependentFilterExpression<T>
{
    public override T Evaluate(IFilterable f)
    {
        if (UserDependent && f is not IUserDependentFilterable)
            throw new ArgumentException("User Dependent Filter was given an IFilterable, rather than an IUserDependentFilterable");

        return Evaluate((IUserDependentFilterable)f);
    }

    public abstract T Evaluate(IUserDependentFilterable f);
}
