using System;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastWatchedDateSortingSelector : UserDependentSortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(UserDependentFilterable f)
    {
        return f.LastWatchedDate ?? DefaultValue;
    }
}
