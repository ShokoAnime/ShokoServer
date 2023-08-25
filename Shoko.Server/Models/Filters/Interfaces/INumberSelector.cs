using System;

namespace Shoko.Server.Models.Filters.Interfaces;

public interface INumberSelector : IFilterSelector
{
    Func<IFilterable, double> Selector { get; }
}
