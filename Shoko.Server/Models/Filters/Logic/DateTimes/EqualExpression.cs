using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.DateTimes;

public class EqualExpression : FilterExpression
{
    public IDateTimeSelector Selector { get; set; }
    public DateTime Parameter { get; set; }
    public override bool UserDependent => Selector.UserDependent;
    public override bool Evaluate(IFilterable filterable) => (Selector.Selector(filterable) - Parameter)?.TotalDays < 1;
}
