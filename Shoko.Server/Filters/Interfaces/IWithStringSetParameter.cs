using System.Collections.Generic;

namespace Shoko.Server.Filters.Interfaces;

public interface IWithStringSetParameter
{
    IReadOnlySet<string> Parameter { get; set; }
}
