using System;
using Moq;
using Shoko.Abstractions.User;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.Tests.Repositories;

/// <summary>
///   A token's use is written at most once per five minutes, and a stored
///   token is handed out with its ID and times.
/// </summary>
public class AuthTokenUseTests
{
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void AUse_IsDue_WhenNeverRecorded()
        => Assert.True(AuthTokensRepository.IsUseDue(null, _now));

    [Theory]
    [InlineData(0, false)]
    [InlineData(299, false)]
    [InlineData(300, true)]
    [InlineData(3600, true)]
    public void AUse_IsDue_OnlyOnceTheRecordedOneIsFiveMinutesOld(int secondsAgo, bool due)
        => Assert.Equal(due, AuthTokensRepository.IsUseDue(_now.AddSeconds(-secondsAgo), _now));

    [Fact]
    public void AStoredToken_KeepsItsIDAndTimes()
    {
        var user = Mock.Of<IUser>();
        var stored = new AuthTokens
        {
            AuthID = 42,
            UserID = 1,
            DeviceName = "desktop",
            Token = "key",
            CreatedAt = _now.AddDays(-1),
            LastUsedAt = _now,
        };

        var token = stored.ToApiToken(user);

        Assert.Equal((42, "desktop", "key", _now.AddDays(-1), _now), (token.ID, token.Device, token.Token, token.CreatedAt, token.LastUsedAt));
        Assert.DoesNotContain("key", token.ToString());
    }
}
