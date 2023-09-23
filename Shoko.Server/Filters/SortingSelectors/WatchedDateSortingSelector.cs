using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class WatchedDateSortingSelector : UserDependentSortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(IUserDependentFilterable f)
    {
        return f.WatchedDate ?? DefaultValue;
    }
}
