using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Functions;

public class TodayFunction : FilterExpression<DateTime?>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;

    public override DateTime? Evaluate(IFilterable f) => DateTime.Today;
}
