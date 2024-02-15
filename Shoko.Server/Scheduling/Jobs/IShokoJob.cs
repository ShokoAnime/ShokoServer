using Quartz;
using Shoko.Commons.Queue;

namespace Shoko.Server.Scheduling.Jobs;

public interface IShokoJob : IJob
{
    string Name { get; }
    QueueStateStruct Description { get; }
}
