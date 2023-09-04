using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class LastAddedDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override DateTime? Evaluate(IFilterable f) => f.LastAddedDate;
}
