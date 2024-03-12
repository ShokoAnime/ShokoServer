using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs;

[JobKeyGroup(JobKeyGroup.System)]
public class TestDelayJob : BaseJob
{
    public int Offset { get; set; }
    public int DelaySeconds { get; set; } = 60;
    public override string TypeName => "Test spin/wait";
    public override string Title => $"Waiting for {DelaySeconds} seconds";

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> {Time} seconds", nameof(TestDelayJob), DelaySeconds);
        return Task.Delay(TimeSpan.FromSeconds(DelaySeconds));
    }
}
