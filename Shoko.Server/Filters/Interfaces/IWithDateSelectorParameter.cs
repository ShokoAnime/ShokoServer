using System;

namespace Shoko.Server.Filters.Interfaces;

public interface IWithDateSelectorParameter
{
    FilterExpression<DateTime?> Left { get; set; }
}
