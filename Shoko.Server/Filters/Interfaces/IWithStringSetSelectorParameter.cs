using System.Collections.Generic;

namespace Shoko.Server.Filters.Interfaces;

public interface IWithStringSetSelectorParameter
{
    FilterExpression<IReadOnlySet<string>> Left { get; set; }
}
