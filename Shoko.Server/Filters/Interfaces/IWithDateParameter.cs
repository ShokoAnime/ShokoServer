using System;

namespace Shoko.Server.Filters.Interfaces;

public interface IWithDateParameter
{
    DateTime Parameter { get; set; }
}
