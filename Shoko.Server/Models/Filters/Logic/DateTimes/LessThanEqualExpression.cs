using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.DateTimes;

public class LessThanEqualExpression : FilterExpression
{
    public IDateTimeSelector Selector { get; set; }
    public DateTime Parameter { get; set; }
    public override bool UserDependent => Selector.UserDependent;
    public override bool Evaluate(IFilterable filterable)
    {
        var date = Selector.Selector(filterable);
        if (date == null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch) return false;
        return date.Value.Date <= Parameter;
    }
}
