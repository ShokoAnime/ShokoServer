using System;
using Shoko.Server.Providers.AniDB;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

public class AniDBExtensionsTests
{
    /// <summary>
    /// A local watched date used to reach AniDB as its own wall clock, because
    /// the conversion subtracted a UTC epoch with raw tick arithmetic and
    /// <see cref="DateTime.Kind"/> takes no part in that. Every entry written
    /// that way sits ahead of itself by the local offset.
    /// </summary>
    [Fact]
    public void GetAniDBDateAsSeconds_LocalIsConvertedToUtc_NotSentAsItsWallClock()
    {
        var local = new DateTime(2022, 6, 17, 19, 13, 3, DateTimeKind.Local);

        Assert.Equal(new DateTimeOffset(local).ToUnixTimeSeconds(), AniDBExtensions.GetAniDBDateAsSeconds(local));
    }

    /// <summary>
    /// The same instant is the same instant, however it is labelled.
    /// </summary>
    [Fact]
    public void GetAniDBDateAsSeconds_LocalAndItsUtcEquivalentAgree()
    {
        var local = new DateTime(2022, 6, 17, 19, 13, 3, DateTimeKind.Local);

        Assert.Equal(
            AniDBExtensions.GetAniDBDateAsSeconds(local.ToUniversalTime()),
            AniDBExtensions.GetAniDBDateAsSeconds(local)
        );
    }

    /// <summary>
    /// Air dates arrive as an unspecified midnight. Converting one would move it
    /// by the local offset and, east of UTC, report the day before.
    /// </summary>
    [Fact]
    public void GetAniDBDateAsSeconds_UnspecifiedIsTakenAtFaceValue()
    {
        var airDate = new DateTime(2022, 6, 17, 0, 0, 0, DateTimeKind.Unspecified);
        var asUtc = new DateTime(2022, 6, 17, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(AniDBExtensions.GetAniDBDateAsSeconds(asUtc), AniDBExtensions.GetAniDBDateAsSeconds(airDate));
    }

    [Fact]
    public void GetAniDBDateAsSeconds_NullIsZero()
    {
        Assert.Equal(0, AniDBExtensions.GetAniDBDateAsSeconds(null));
    }
}
