using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;

namespace Shoko.Server.Scheduling.Jobs;

[JobKeyGroup(JobKeyGroup.System)]
public class TestDelayJob : BaseJob
{
    public int Offset { get; set; }
    public int DelaySeconds { get; set; } = 60;
    public override string Name => "Test spin/wait";

    public override QueueStateStruct Description => new()
    {
        message = "{0} second spin/wait",
        queueState = QueueStateEnum.Refresh,
        extraParams = new[] { DelaySeconds.ToString() }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> {Time} seconds", nameof(TestDelayJob), DelaySeconds);
        return Task.Delay(TimeSpan.FromSeconds(DelaySeconds));
    }
}
