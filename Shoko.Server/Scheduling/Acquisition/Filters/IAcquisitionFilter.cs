using System;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public interface IAcquisitionFilter
{
    Type[] GetTypesToExclude();
}
