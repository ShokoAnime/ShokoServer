using System;

namespace Shoko.Server.Filters.Interfaces;

public interface IWithSecondDateSelectorParameter
{
    FilterExpression<DateTime?> Right { get; set; }
}
