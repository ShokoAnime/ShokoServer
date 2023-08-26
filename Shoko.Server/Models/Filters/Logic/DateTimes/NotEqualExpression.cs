using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.DateTimes;

public class NotEqualExpression : FilterExpression<bool>
{
    public FilterExpression<DateTime?> Selector { get; set; }
    public DateTime Parameter { get; set; }
    public override bool TimeDependent => Selector.TimeDependent;
    public override bool UserDependent => Selector.UserDependent;
    public override bool Evaluate(IFilterable filterable) => (Selector.Evaluate(filterable) - Parameter)?.TotalDays >= 1;
}
