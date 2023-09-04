using System;

namespace Shoko.Server.Filters.Selectors;

public class AddedDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override DateTime? Evaluate(Filterable f)
    {
        return f.AddedDate;
    }
}
