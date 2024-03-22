using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Test;

[JobKeyGroup(JobKeyGroup.System)]
public class TestErrorJob : BaseJob
{
    public int Offset { get; set; }
    public override string TypeName => "Test Error";
    public override string Title => "Throwing an Error";

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(TestErrorJob));
        throw new Exception("TEST TEST TEST ERROR!!!");
    }
}
