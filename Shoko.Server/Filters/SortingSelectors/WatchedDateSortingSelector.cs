using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class WatchedDateSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This sorts by the first date that a filterable was watched by the current user";
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.WatchedDate ?? DefaultValue;
    }
}
