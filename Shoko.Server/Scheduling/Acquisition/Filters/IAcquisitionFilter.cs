using System;
using System.Collections.Generic;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public interface IAcquisitionFilter
{
    IEnumerable<Type> GetTypesToExclude();
    event EventHandler StateChanged;
}
