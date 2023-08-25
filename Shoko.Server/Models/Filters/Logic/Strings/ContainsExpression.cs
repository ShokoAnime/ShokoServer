using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.Strings;

public class ContainsExpression : FilterExpression
{
    public IStringSelector Selector { get; set; }
    public string Parameter { get; set; }
    public override bool UserDependent => Selector.UserDependent;

    public override bool Evaluate(IFilterable filterable) => Parameter.Contains(Selector.Selector(filterable), StringComparison.InvariantCultureIgnoreCase);
}
