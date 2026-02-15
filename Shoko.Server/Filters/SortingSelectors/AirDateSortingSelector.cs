using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class AirDateSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the date that a filterable first aired";
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AirDate ?? DefaultValue;
    }
}
