using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Functions;

public class TodayFunction : FilterExpression<DateTime?>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;

    public override DateTime? Evaluate(IFilterable f) => DateTime.Today;
}
