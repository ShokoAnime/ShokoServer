using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastWatchedDateSortingSelector : SortingExpression
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This sorts by the last date that a filterable was watched by the current user";
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.LastWatchedDate ?? DefaultValue;
    }
}
