using System;

namespace Shoko.Server.Filters.Selectors;

public class AirDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override DateTime? Evaluate(Filterable f)
    {
        return f.AirDate;
    }
}
