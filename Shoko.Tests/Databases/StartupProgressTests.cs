using System;
using System.Collections.Generic;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Unit tests for <see cref="StartupProgress"/>, which throttles the progress
/// messages of long startup steps.
/// </summary>
public sealed class StartupProgressTests
{
    #region Helpers

    private sealed class ManualTimeProvider : TimeProvider
    {
        public long Ticks { get; set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
            => Ticks;

        public void Advance(TimeSpan time)
            => Ticks += time.Ticks;
    }

    #endregion

    #region Tenths

    [Fact]
    public void Advance_ReportsOncePerTenth_AndNeverTheEnd()
    {
        var messages = new List<string>();
        var progress = new StartupProgress(messages.Add, "Working", 1000, new ManualTimeProvider());

        for (var index = 0; index < 1000; index++)
            progress.Advance();

        Assert.Equal(9, messages.Count);
        Assert.Equal("Working... 100/1000", messages[0]);
        Assert.Equal("Working... 900/1000", messages[^1]);
    }

    [Fact]
    public void Advance_ReportsAJumpOverSeveralTenthsOnce()
    {
        var messages = new List<string>();
        var progress = new StartupProgress(messages.Add, "Working", 1000, new ManualTimeProvider());

        progress.Advance(450);
        progress.Advance(10);

        Assert.Equal(["Working... 450/1000"], messages);
    }

    #endregion

    #region Interval

    [Fact]
    public void Advance_ReportsWithinATenth_OnceTheIntervalHasPassed()
    {
        var messages = new List<string>();
        var time = new ManualTimeProvider();
        var progress = new StartupProgress(messages.Add, "Working", 1000, time);

        progress.Advance();
        time.Advance(StartupProgress.Interval - TimeSpan.FromMilliseconds(1));
        progress.Advance();
        time.Advance(TimeSpan.FromMilliseconds(1));
        progress.Advance();
        progress.Advance();

        Assert.Equal(["Working... 3/1000"], messages);
    }

    #endregion
}
