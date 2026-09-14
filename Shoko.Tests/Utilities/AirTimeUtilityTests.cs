using System;
using System.Collections.Generic;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

public class AirTimeUtilityTests
{
    private static (DateTime, DateTime) Sample(int day, int hour, int minute = 0)
        => (new DateTime(2026, 7, day), new DateTime(2026, 7, day, hour, minute, 0, DateTimeKind.Utc));

    [Fact]
    public void LearnAirTimeOffset_NeedsAtLeastTwoSamples()
    {
        Assert.Null(AirTimeUtility.LearnAirTimeOffset([]));
        Assert.Null(AirTimeUtility.LearnAirTimeOffset([Sample(2, 16, 28)]));
    }

    [Fact]
    public void LearnAirTimeOffset_ReturnsTheSharedSlot()
    {
        var offset = AirTimeUtility.LearnAirTimeOffset([Sample(2, 16, 28), Sample(9, 16, 28), Sample(16, 16, 28)]);

        Assert.Equal(new TimeSpan(16, 28, 0), offset);
    }

    [Fact]
    public void LearnAirTimeOffset_IgnoresAOneOffSlotThroughTheMedian()
    {
        // Episode 9 aired in a different slot; the median still lands on the regular one.
        var offset = AirTimeUtility.LearnAirTimeOffset([Sample(2, 16, 28), Sample(9, 3, 0), Sample(16, 16, 28)]);

        Assert.Equal(new TimeSpan(16, 28, 0), offset);
    }

    [Fact]
    public void LearnAirTimeOffset_CrossesMidnightWhenTheSlotIsOnTheNextUtcDay()
    {
        // A late-night JST slot lands on the following UTC day; the offset is simply more than a day.
        var samples = new List<(DateTime, DateTime)>
        {
            (new DateTime(2026, 7, 2), new DateTime(2026, 7, 3, 1, 30, 0, DateTimeKind.Utc)),
            (new DateTime(2026, 7, 9), new DateTime(2026, 7, 10, 1, 30, 0, DateTimeKind.Utc)),
        };

        var offset = AirTimeUtility.LearnAirTimeOffset(samples);

        Assert.Equal(new TimeSpan(25, 30, 0), offset);
        Assert.Equal(new DateTime(2026, 7, 24, 1, 30, 0, DateTimeKind.Utc), AirTimeUtility.EstimateAirTime(new DateTime(2026, 7, 23), offset!.Value));
    }

    [Fact]
    public void LearnAirTimeOffset_OnlyLooksAtTheMostRecentWindow()
    {
        // Twelve old episodes in one slot, then the series moved; with a window of two only the new slot counts.
        var samples = new List<(DateTime, DateTime)>();
        for (var day = 1; day <= 12; day++)
            samples.Add((new DateTime(2026, 1, day), new DateTime(2026, 1, day, 9, 0, 0, DateTimeKind.Utc)));
        samples.Add(Sample(2, 16, 28));
        samples.Add(Sample(9, 16, 28));

        Assert.Equal(new TimeSpan(16, 28, 0), AirTimeUtility.LearnAirTimeOffset(samples, window: 2));
        Assert.Equal(new TimeSpan(9, 0, 0), AirTimeUtility.LearnAirTimeOffset(samples, window: 14));
    }

    [Fact]
    public void EstimateAirTime_IsUtc()
    {
        var estimate = AirTimeUtility.EstimateAirTime(new DateTime(2026, 7, 23), new TimeSpan(16, 28, 0));

        Assert.Equal(DateTimeKind.Utc, estimate.Kind);
        Assert.Equal(new DateTime(2026, 7, 23, 16, 28, 0), estimate);
    }
}
