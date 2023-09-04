using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastWatchedDateSortingSelector : UserDependentSortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public DateTime DefaultValue { get; set; }
    public override object Evaluate(IUserDependentFilterable f) => f.LastWatchedDate ?? DefaultValue;
}
