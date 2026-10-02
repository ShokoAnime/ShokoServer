using System;
using System.Collections.Generic;
using Shoko.Abstractions.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

/// <summary>
/// Covers the progress helpers plugins and core use for scheduled actions:
/// staged and item progress never go down and end at 100, slices map and
/// clamp, and the throttle lets the first and last values through.
/// </summary>
public sealed class ProgressHelperTests
{
    #region Helpers

    /// <summary>
    /// Keeps every value reported, in order.
    /// </summary>
    private sealed class ListProgress : IProgress<decimal>
    {
        public List<decimal> Values { get; } = [];

        public void Report(decimal value) => Values.Add(value);
    }

    /// <summary>
    /// A clock moved by hand.
    /// </summary>
    private sealed class ManualTime : TimeProvider
    {
        public long Ticks { get; set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Ticks;

        public void Advance(TimeSpan by) => Ticks += by.Ticks;
    }

    #endregion

    #region Staged

    [Fact]
    public void StagedProgress_MapsEachStageIntoItsWeightedShare()
    {
        var parent = new ListProgress();
        var stages = new StagedProgress(parent, 1m, 3m);

        stages.Report(0);
        stages.Report(100);
        stages.NextStage();
        stages.Report(50);
        stages.Complete();

        Assert.Equal([0m, 25m, 62.5m, 100m], parent.Values);
    }

    [Fact]
    public void StagedProgress_NeverGoesDown_AndIgnoresReportsAfterCompletion()
    {
        var parent = new ListProgress();
        var stages = new StagedProgress(parent, 2);

        stages.Report(80);
        stages.Report(20);
        stages.Report(-50);
        stages.Complete();
        stages.Report(10);

        Assert.Equal([40m, 100m], parent.Values);
    }

    #endregion

    #region Items

    [Fact]
    public void ItemProgress_ReportsTheShareDone_UpTo100()
    {
        var parent = new ListProgress();
        var items = new ItemProgress(parent, 4);

        items.Report(0);
        items.Increment();
        items.Increment(2);
        items.Report(1);
        items.Increment(5);

        Assert.Equal([0m, 25m, 75m, 100m], parent.Values);
        Assert.Equal(4, items.Done);
    }

    [Fact]
    public void ItemProgress_WithNoItems_IsDone()
    {
        var parent = new ListProgress();

        new ItemProgress(parent, 0).Report(0);

        Assert.Equal([100m], parent.Values);
    }

    #endregion

    #region Range

    [Fact]
    public void RangeProgress_MapsAndClampsIntoItsSlice()
    {
        var parent = new ListProgress();
        var slice = parent.Slice(20, 60);

        slice.Report(-10);
        slice.Report(50);
        slice.Report(150);

        Assert.Equal([20m, 40m, 60m], parent.Values);
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.Slice(60, 20));
    }

    #endregion

    #region Throttled

    [Fact]
    public void ThrottledProgress_PassesTheFirstAndTheLast_AndHoldsBackTheRest()
    {
        var parent = new ListProgress();
        var time = new ManualTime();
        var throttle = new ThrottledProgress(parent, TimeSpan.FromSeconds(1), time);

        for (var done = 0; done < 30_000; done++)
            throttle.Report(100m * done / 30_000);
        throttle.Report(100);

        Assert.Equal([0m, 100m], parent.Values);
    }

    [Fact]
    public void ThrottledProgress_PassesAValueOnceTheIntervalIsUp_AndFlushesWhatWasHeldBack()
    {
        var parent = new ListProgress();
        var time = new ManualTime();
        var throttle = new ThrottledProgress(parent, TimeSpan.FromSeconds(1), time);

        throttle.Report(10);
        throttle.Report(20);
        time.Advance(TimeSpan.FromSeconds(1));
        throttle.Report(30);
        throttle.Report(40);
        throttle.Flush();
        throttle.Flush();

        Assert.Equal([10m, 30m, 40m], parent.Values);
    }

    #endregion
}
