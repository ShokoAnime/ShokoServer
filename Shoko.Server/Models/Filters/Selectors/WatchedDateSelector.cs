using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class WatchedDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override DateTime? Evaluate(IFilterable f) => f.WatchedDate;
}
