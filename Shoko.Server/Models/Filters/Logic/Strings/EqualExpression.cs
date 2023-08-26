using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.Strings;

public class EqualExpression : FilterExpression<bool>
{
    public FilterExpression<string> Selector { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Selector.TimeDependent;
    public override bool UserDependent => Selector.UserDependent;

    public override bool Evaluate(IFilterable filterable) =>
        string.Equals(Selector.Evaluate(filterable), Parameter, StringComparison.InvariantCultureIgnoreCase);
}
