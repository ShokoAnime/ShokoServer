using System.Diagnostics;
using OpenTelemetry.Trace;
using Shoko.QueueProcessor.Analytics;
using Shoko.Server.Telemetry;
using Xunit;

namespace Shoko.Tests.Telemetry;

public class TelemetrySamplerTests
{
    private static readonly string JobSpan = QueueTelemetry.JobSpanPrefix + "SomeJob";

    private static SamplingDecision Decide(double traceRatio, double jobRatio, string name, ActivityContext parent = default)
        => new TelemetrySampler(traceRatio, jobRatio).ShouldSample(new(parent, ActivityTraceId.CreateRandom(), name, ActivityKind.Internal)).Decision;

    private static ActivityContext Parent(ActivityTraceFlags flags)
        => new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), flags);

    [Fact]
    public void JobRoots_UseTheJobRatio()
    {
        Assert.Equal(SamplingDecision.Drop, Decide(1, 0, JobSpan));
        Assert.Equal(SamplingDecision.RecordAndSample, Decide(0, 1, JobSpan));
    }

    [Fact]
    public void OtherRoots_UseTheTraceRatio()
    {
        Assert.Equal(SamplingDecision.RecordAndSample, Decide(1, 0, "GET /api/v3/Series"));
        Assert.Equal(SamplingDecision.Drop, Decide(0, 1, "GET /api/v3/Series"));
    }

    [Theory]
    [InlineData("http.client")]
    [InlineData("shoko.job SomeJob")]
    public void Children_FollowTheirParentWhateverTheRatios(string name)
    {
        Assert.Equal(SamplingDecision.RecordAndSample, Decide(0, 0, name, Parent(ActivityTraceFlags.Recorded)));
        Assert.Equal(SamplingDecision.Drop, Decide(1, 1, name, Parent(ActivityTraceFlags.None)));
    }

    [Fact]
    public void ARatioBetween_KeepsAboutThatShare()
    {
        var sampler = new TelemetrySampler(1, 0.25);
        var kept = 0;
        for (var i = 0; i < 20000; i++)
        {
            if (sampler.ShouldSample(new(default, ActivityTraceId.CreateRandom(), JobSpan, ActivityKind.Consumer)).Decision is SamplingDecision.RecordAndSample)
                kept++;
        }

        Assert.InRange(kept, 4400, 5600);
    }
}
