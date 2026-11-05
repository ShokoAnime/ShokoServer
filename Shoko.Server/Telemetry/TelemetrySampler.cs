using OpenTelemetry.Trace;
using Shoko.QueueProcessor.Analytics;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Keeps a share of the traces, with a share of its own for queue jobs, since a large import can run tens of thousands
/// of them. A span with a parent follows its parent, so a job that is not kept keeps none of its children either.
/// </summary>
/// <remarks>
/// Jobs are told apart by their span's name, because the sampler is not told which source a span is from.
/// </remarks>
internal sealed class TelemetrySampler(double traceRatio, double jobRatio) : Sampler
{
    private readonly ParentBasedSampler _traces = new(new TraceIdRatioBasedSampler(traceRatio));

    private readonly ParentBasedSampler _jobs = new(new TraceIdRatioBasedSampler(jobRatio));

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        => samplingParameters.Name.StartsWith(QueueTelemetry.JobSpanPrefix, System.StringComparison.Ordinal)
            ? _jobs.ShouldSample(samplingParameters)
            : _traces.ShouldSample(samplingParameters);
}
