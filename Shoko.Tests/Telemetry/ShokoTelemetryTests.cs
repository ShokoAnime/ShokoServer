using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using Shoko.Server.Telemetry;
using Xunit;

namespace Shoko.Tests.Telemetry;

/// <summary>
/// The meter and source are static and shared with the rest of the suite, so each test only keeps what was recorded on its
/// own thread, or under a name of its own.
/// </summary>
public class ShokoTelemetryTests
{
    private static string NewName() => Guid.NewGuid().ToString("N");

    private static List<(string Instrument, Dictionary<string, object?> Tags)> Record(Action action)
    {
        var thread = Environment.CurrentManagedThreadId;
        var recorded = new List<(string, Dictionary<string, object?>)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ShokoTelemetry.ServerSourceName && !instrument.IsObservable)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Keep(instrument, tags));
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Keep(instrument, tags));
        listener.Start();
        action();
        return recorded;

        void Keep(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Environment.CurrentManagedThreadId == thread)
                recorded.Add((instrument.Name, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
        }
    }

    [Fact]
    public void AHashProviderFailing_IsRecordedWithTheErrorsType()
    {
        var provider = NewName();

        var (instrument, tags) = Assert.Single(Record(() => ShokoTelemetry.RecordHashDuration(provider, Stopwatch.GetTimestamp(), new InvalidOperationException("/secret/path"))));

        Assert.Equal("shoko.video.hash.duration", instrument);
        Assert.Equal(provider, tags["shoko.video.hash.provider"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["error.type"]);
        Assert.DoesNotContain(tags.Values, value => value is string text && text.Contains("/secret/path"));
    }

    [Fact]
    public void AReleaseSearch_IsRecordedByProviderAndWhetherItFoundAnything()
    {
        var provider = NewName();

        var (instrument, tags) = Assert.Single(Record(() => ShokoTelemetry.RecordReleaseSearch(provider, Stopwatch.GetTimestamp(), found: true)));

        Assert.Equal("shoko.release.search.duration", instrument);
        Assert.Equal(provider, tags["shoko.release.provider"]);
        Assert.Equal(true, tags["shoko.release.found"]);
        Assert.False(tags.ContainsKey("error.type"));
    }

    [Fact]
    public void AnImageDownload_IsRecordedBySource()
    {
        var source = NewName();

        var (instrument, tags) = Assert.Single(Record(() => ShokoTelemetry.RecordImageDownload(source, Stopwatch.GetTimestamp())));

        Assert.Equal("shoko.image.download.duration", instrument);
        Assert.Equal(source, tags["shoko.image.source"]);
    }

    [Fact]
    public void AFileThatHasGoneAway_IsNotAnError()
    {
        var missing = new FileInfo(Path.Combine(Path.GetTempPath(), NewName()));

        Assert.Empty(Record(() => ShokoTelemetry.RecordHashedFile(null, missing)));
    }

    [Fact]
    public void AHashedFile_ReportsOnlyItsSize()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[1234]);
            var (instrument, tags) = Assert.Single(Record(() => ShokoTelemetry.RecordHashedFile(null, new(path))));

            Assert.Equal("shoko.video.hash.size", instrument);
            Assert.Empty(tags);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AThrowingListener_DoesNotReachTheCaller()
    {
        var thread = Environment.CurrentManagedThreadId;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ShokoTelemetry.ServerSourceName && !instrument.IsObservable)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, _, _, _) =>
        {
            if (Environment.CurrentManagedThreadId == thread)
                throw new InvalidOperationException("A listener failed.");
        });
        listener.Start();

        ShokoTelemetry.RecordImageDownload(NewName(), Stopwatch.GetTimestamp());
    }

    [Fact]
    public void FinishingASpanWithAFailure_KeepsOnlyTheErrorsType()
    {
        using var span = new Activity("test").Start();

        ShokoTelemetry.FinishSpan(span, new InvalidOperationException("/secret/path"));

        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal(typeof(InvalidOperationException).FullName, span.GetTagItem("error.type"));
    }
}
