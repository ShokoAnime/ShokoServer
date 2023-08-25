using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class AddedDateSelector : IDateTimeSelector
{
    public bool UserDependent => false;
    public Func<IFilterable, DateTime?> Selector => f => f.AddedDate;
}
