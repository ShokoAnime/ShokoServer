using System;

namespace Shoko.Server.Filters.Functions;

public class DateAddFunction : FilterExpression<DateTime?>
{
    public DateAddFunction()
    {
    }

    public DateAddFunction(FilterExpression<DateTime?> selector, TimeSpan parameter)
    {
        Selector = selector;
        Parameter = parameter;
    }

    public FilterExpression<DateTime?> Selector { get; set; }
    public TimeSpan Parameter { get; set; }

    public override bool TimeDependent => Selector.TimeDependent;
    public override bool UserDependent => Selector.UserDependent;

    public override DateTime? Evaluate(Filterable f)
    {
        return Selector.Evaluate(f) + Parameter;
    }
}
