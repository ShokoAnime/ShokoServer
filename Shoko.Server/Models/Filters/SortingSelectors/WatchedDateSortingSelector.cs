using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.SortingSelectors;

public class WatchedDateSortingSelector : UserDependentSortingExpression<DateTime>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public DateTime DefaultValue { get; set; }
    public override DateTime Evaluate(IUserDependentFilterable f) => f.WatchedDate ?? DefaultValue;
}
