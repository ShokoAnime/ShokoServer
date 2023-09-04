using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class LastWatchedDateSelector : UserDependentFilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override DateTime? Evaluate(IUserDependentFilterable f) => f.LastWatchedDate;
}
