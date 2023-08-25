using System;

namespace Shoko.Server.Models.Filters.Interfaces;

public interface IStringSelector : IFilterSelector
{
    Func<IFilterable, string> Selector { get; }
}
