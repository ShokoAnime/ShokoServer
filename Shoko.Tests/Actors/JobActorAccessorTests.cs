using System;
using Moq;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Actors;

/// <summary>
///   The queue stores the user and device of the actor, and the token is looked up again when
///   the job runs, so a token revoked since, or a user removed since, runs it for the system.
/// </summary>
public class JobActorAccessorTests
{
    private static readonly JMMUser _user = new() { JMMUserID = 3, Username = "carol" };

    private static JobActorAccessor Create(params AuthTokens[] tokens)
        => Create(new Mock<ISystemService>().Object, tokens);

    private static JobActorAccessor Create(ISystemService systemService, params AuthTokens[] tokens)
        => new(
            systemService,
            new(() => CachedRepo.Build<JMMUserRepository, int, JMMUser>(u => u.JMMUserID, _user)),
            new(() => CachedRepo.Build<AuthTokensRepository, int, AuthTokens>(t => t.AuthID, tokens))
        );

    private static AuthTokens Stored(int id, string device, DateTime? expiresAt = null)
        => new() { AuthID = id, UserID = _user.JMMUserID, DeviceName = device, Token = Guid.NewGuid().ToString(), ExpiresAt = expiresAt };

    [Fact]
    public void Capture_TakesTheUserAndDeviceOnly()
    {
        var accessor = Create();
        var token = ActorContextTests.Token("phone", userID: 3);

        using (ActorContext.Begin(token))
            Assert.Equal(new JobActor(3, "phone"), accessor.Capture());
        Assert.Null(accessor.Capture());
    }

    [Fact]
    public void Restore_LooksTheTokenUpAgain()
    {
        var stored = Stored(1, "phone");
        var accessor = Create(Stored(2, "desktop"), stored);

        using (accessor.Restore(new JobActor(3, "phone")))
        {
            var current = ActorContext.CurrentActor;
            Assert.NotNull(current);
            Assert.Same(_user, current.User);
            Assert.Equal("phone", current.Device);
            Assert.Equal(stored.Token, current.Token);
        }

        Assert.Null(ActorContext.CurrentActor);
    }

    /// <summary>
    /// Covers a token revoked or expired since the job was queued, and a user removed since.
    /// </summary>
    /// <param name="storedDevice">The device of the one token left stored for the user.</param>
    /// <param name="expired">Whether that token has expired.</param>
    /// <param name="userID">The user the job was queued for.</param>
    [Theory]
    [InlineData("desktop", false, 3)]
    [InlineData("phone", true, 3)]
    [InlineData("phone", false, 4)]
    public void Restore_NoLongerValidSince_RunsForTheSystem(string storedDevice, bool expired, int userID)
    {
        var accessor = Create(Stored(1, storedDevice, expired ? DateTime.Now.AddMinutes(-1) : null));

        using (ActorContext.Begin(ActorContextTests.Token("outer")))
        using (accessor.Restore(new JobActor(userID, "phone")))
            Assert.Null(ActorContext.CurrentActor);
    }

    [Fact]
    public void Restore_NoActor_RunsForTheSystemWhateverTheFlowHeld()
    {
        var accessor = Create();

        using (ActorContext.Begin(ActorContextTests.Token("worker-start")))
        using (accessor.Restore(null))
            Assert.Null(ActorContext.CurrentActor);
    }

    /// <summary>
    /// The repositories cannot be read while the database is blocked, which at start-up is before
    /// their caches are loaded, so the queue must hold jobs with an actor until it is not.
    /// </summary>
    [Fact]
    public void CanRestore_FollowsTheDatabaseBlock()
    {
        var systemService = new Mock<ISystemService>();
        systemService.SetupGet(s => s.IsDatabaseBlocked).Returns(true);
        var userRepositoryBuilt = false;
        var accessor = new JobActorAccessor(
            systemService.Object,
            new(() =>
            {
                userRepositoryBuilt = true;
                return CachedRepo.Build<JMMUserRepository, int, JMMUser>(u => u.JMMUserID, _user);
            }),
            new(() => CachedRepo.Build<AuthTokensRepository, int, AuthTokens>(t => t.AuthID))
        );
        var changes = 0;
        accessor.CanRestoreChanged += (_, _) => changes++;

        Assert.False(accessor.CanRestore);

        systemService.SetupGet(s => s.IsDatabaseBlocked).Returns(false);
        systemService.Raise(s => s.DatabaseBlockedChanged += null, new DatabaseBlockedChangedEventArgs { IsBlocked = false });

        Assert.True(accessor.CanRestore);
        Assert.Equal(1, changes);
        Assert.False(userRepositoryBuilt);
    }
}
