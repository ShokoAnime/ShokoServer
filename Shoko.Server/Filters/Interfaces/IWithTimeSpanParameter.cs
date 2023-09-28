using System;

namespace Shoko.Server.Filters.Interfaces;

public interface IWithTimeSpanParameter
{
    TimeSpan Parameter { get; set; }
}
