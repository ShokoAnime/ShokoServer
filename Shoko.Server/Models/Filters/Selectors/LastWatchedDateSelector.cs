using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class LastWatchedDateSelector : IDateTimeSelector
{
    public bool UserDependent => true;
    public Func<IFilterable, DateTime?> Selector => f => f.LastWatchedDate;
}
