using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class LastWatchedDateSelector : UserDependentFilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override DateTime? Evaluate(IUserDependentFilterable f) => f.LastWatchedDate;
}
