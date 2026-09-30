using System;
using Shoko.Abstractions.Extensions;
using Xunit;

namespace Shoko.Tests.Extensions;

/// <summary>
/// Covers how time spans are written out, as a duration and as a time ago.
/// </summary>
public class TimeSpanExtensionsTests
{
    #region Duration

    [Theory]
    [InlineData(0, "0 minutes")]
    [InlineData(59, "59 seconds")]
    [InlineData(61, "1 minute 1 second")]
    [InlineData(90061, "1 day 1 hour 1 minute 1 second")]
    [InlineData(172860, "2 days 1 minute")]
    [InlineData(31622400, "366 days")]
    public void ADuration_ListsItsPartsThatAreNotZero(int seconds, string expected)
        => Assert.Equal(expected, TimeSpan.FromSeconds(seconds).ToDurationString());

    [Fact]
    public void ADuration_DropsFractionsOfASecond()
    {
        Assert.Equal("0 minutes", TimeSpan.FromMilliseconds(999).ToDurationString());
        Assert.Equal("1 minute", TimeSpan.FromMilliseconds(60_500).ToDurationString());
    }

    [Fact]
    public void ANegativeDuration_ReadsAsItsLength()
    {
        Assert.Equal("6 hours", TimeSpan.FromHours(-6).ToDurationString());
        Assert.Equal(TimeSpan.MaxValue.ToDurationString(), TimeSpan.MinValue.ToDurationString());
    }

    #endregion

    #region Time Ago

    [Theory]
    [InlineData(59, "less than a minute ago")]
    [InlineData(89, "1 minute ago")]
    [InlineData(90, "2 minutes ago")]
    [InlineData(3595, "1 hour ago")]
    [InlineData(7500, "2 hours 5 minutes ago")]
    [InlineData(86700, "1 day 5 minutes ago")]
    [InlineData(276659, "3 days 4 hours ago")]
    public void ATimeAgo_ShowsItsTwoLargestParts(int seconds, string expected)
        => Assert.Equal(expected, TimeSpan.FromSeconds(seconds).ToTimeAgoString());

    [Fact]
    public void ANegativeTimeAgo_IsLessThanAMinute()
    {
        Assert.Equal("less than a minute ago", TimeSpan.FromHours(-2).ToTimeAgoString());
        Assert.Equal("less than a minute ago", TimeSpan.MinValue.ToTimeAgoString());
    }

    [Fact]
    public void TheLongestTimeAgo_DoesNotOverflow()
        => Assert.EndsWith(" ago", TimeSpan.MaxValue.ToTimeAgoString());

    #endregion
}
