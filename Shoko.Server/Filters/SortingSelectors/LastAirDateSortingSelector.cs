using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastAirDateSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the last date that a filterable aired";
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.LastAirDate ?? DefaultValue;
    }
}
