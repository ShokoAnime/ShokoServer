using System;

namespace Shoko.Server.Filters.Functions;

public class TodayFunction : FilterExpression<DateTime?>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;

    public override DateTime? Evaluate(Filterable f)
    {
        return DateTime.Today;
    }
}
