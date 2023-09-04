using System;

namespace Shoko.Server.Filters.Functions;

public class DateDiffFunction : FilterExpression<DateTime?>
{
    public DateDiffFunction(FilterExpression<DateTime?> selector, TimeSpan parameter)
    {
        Selector = selector;
        Parameter = parameter;
    }
    public DateDiffFunction() { }

    public FilterExpression<DateTime?> Selector { get; set; }
    public TimeSpan Parameter { get; set; }

    public override bool TimeDependent => Selector.TimeDependent;
    public override bool UserDependent => Selector.UserDependent;

    public override DateTime? Evaluate(Filterable f)
    {
        return Selector.Evaluate(f) - Parameter;
    }
}
