using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastAirDateSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by the last date that a filterable aired";
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.LastAirDate ?? DefaultValue;
    }
}
