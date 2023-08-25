using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class WatchedDateSelector : IDateTimeSelector
{
    public bool UserDependent => true;
    public Func<IFilterable, DateTime?> Selector => f => f.WatchedDate;
}
