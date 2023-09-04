using System;

namespace Shoko.Server.Filters.Selectors;

public class LastWatchedDateSelector : UserDependentFilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override DateTime? Evaluate(UserDependentFilterable f)
    {
        return f.LastWatchedDate;
    }
}
