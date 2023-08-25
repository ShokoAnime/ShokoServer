using System;

namespace Shoko.Server.Models.Filters.Interfaces;

public interface IDateTimeSelector : IFilterSelector
{
    Func<IFilterable, DateTime?> Selector { get; }
}
