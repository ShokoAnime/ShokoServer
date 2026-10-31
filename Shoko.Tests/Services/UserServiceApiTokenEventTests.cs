using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.User.Events;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   <see cref="UserService.ApiTokenGenerated"/> and <see cref="UserService.ApiTokenInvalidated"/>
///   are raised for new and removed tokens only.
/// </summary>
public class UserServiceApiTokenEventTests
{
    [Fact]
    public async Task Generate_RaisesOnlyForANewToken()
    {
        var harness = new Harness();
        var user = harness.Users[0];

        var first = await harness.Service.GenerateApiTokenForUser(user, "desktop");
        var second = await harness.Service.GenerateApiTokenForUser(user, "desktop");

        Assert.Equal(first.Token, second.Token);
        var generated = Assert.Single(harness.Generated);
        Assert.Same(user, generated.ApiToken.User);
        Assert.Equal("desktop", generated.ApiToken.Device);
        Assert.Equal(first.Token, generated.ApiToken.Token);
        Assert.Empty(harness.Invalidated);
    }

    [Fact]
    public async Task GenerateExpiring_AlwaysRaises()
    {
        var harness = new Harness();
        var user = harness.Users[0];

        await harness.Service.GenerateApiTokenForUser(user, "phone", DateTime.Now.AddHours(1));
        await harness.Service.GenerateApiTokenForUser(user, "phone", DateTime.Now.AddHours(1));

        Assert.Equal(2, harness.Generated.Count);
        Assert.All(harness.Generated, e => Assert.NotNull(e.ApiToken.ExpiresAt));
    }

    [Fact]
    public async Task Generate_RaisesForTheOtherPermanentTokensOfTheDeviceItDrops()
    {
        var harness = new Harness();
        var user = harness.Users[0];
        harness.Tokens.Cache.Update(new() { AuthID = 101, UserID = user.JMMUserID, DeviceName = "desktop", Token = "kept" });
        harness.Tokens.Cache.Update(new() { AuthID = 102, UserID = user.JMMUserID, DeviceName = "Desktop", Token = "dropped" });
        harness.Tokens.Cache.Update(new() { AuthID = 103, UserID = user.JMMUserID, DeviceName = "desktop", Token = "" });

        var token = await harness.Service.GenerateApiTokenForUser(user, "desktop");

        Assert.Equal("kept", token.Token);
        Assert.Equal("dropped", Assert.Single(harness.Invalidated).ApiToken.Token);
        Assert.Empty(harness.Generated);
    }

    [Fact]
    public async Task InvalidateToken_RaisesForTheRemovedToken()
    {
        var harness = new Harness();
        var user = harness.Users[0];
        var token = await harness.Service.GenerateApiTokenForUser(user, "desktop");

        Assert.True(await harness.Service.InvalidateApiToken(token.Token));
        Assert.False(await harness.Service.InvalidateApiToken(token.Token));

        var invalidated = Assert.Single(harness.Invalidated);
        Assert.Equal(token.Token, invalidated.ApiToken.Token);
        Assert.Equal(user.JMMUserID, invalidated.ApiToken.User.LocalID);
    }

    [Fact]
    public async Task InvalidateDevice_RaisesForThatDeviceOnly()
    {
        var harness = new Harness();
        var user = harness.Users[0];
        await harness.Service.GenerateApiTokenForUser(user, "desktop");
        await harness.Service.GenerateApiTokenForUser(user, "phone");

        await harness.Service.InvalidateApiDeviceForUser(user, "phone");

        Assert.Equal("phone", Assert.Single(harness.Invalidated).ApiToken.Device);
    }

    [Fact]
    public async Task InvalidateAllForUser_RaisesForEachToken()
    {
        var harness = new Harness();
        var user = harness.Users[0];
        await harness.Service.GenerateApiTokenForUser(user, "desktop");
        await harness.Service.GenerateApiTokenForUser(user, "phone");
        await harness.Service.GenerateApiTokenForUser(harness.Users[1], "desktop");

        await harness.Service.InvalidateApiTokensForUser(user);

        Assert.Equal(["desktop", "phone"], harness.Invalidated.Select(e => e.ApiToken.Device).Order());
        Assert.All(harness.Invalidated, e => Assert.Same(user, e.ApiToken.User));
    }

    [Fact]
    public async Task DeleteUser_RaisesForEachOfItsTokens()
    {
        var harness = new Harness();
        var user = harness.Users[0];
        await harness.Service.GenerateApiTokenForUser(user, "desktop");
        await harness.Service.GenerateApiTokenForUser(harness.Users[1], "desktop");

        await harness.Service.DeleteUser(user);

        var invalidated = Assert.Single(harness.Invalidated);
        Assert.Same(user, invalidated.ApiToken.User);
    }

    [Fact]
    public void InvalidateExpired_RaisesForEachExpiredToken_AndKeepsTheRest()
    {
        var harness = new Harness();
        var past = DateTime.Now.AddMinutes(-1);
        harness.Tokens.Cache.Update(new() { AuthID = 201, UserID = 1, DeviceName = "web", Token = "11111111-1111-4111-8111-111111111111", ExpiresAt = past });
        harness.Tokens.Cache.Update(new() { AuthID = 202, UserID = 2, DeviceName = "web", Token = "22222222-2222-4222-8222-222222222222", ExpiresAt = past });
        harness.Tokens.Cache.Update(new() { AuthID = 203, UserID = 1, DeviceName = "web", Token = "33333333-3333-4333-8333-333333333333", ExpiresAt = DateTime.Now.AddDays(1) });

        Assert.Equal(2, harness.Service.InvalidateExpiredApiTokens(TestContext.Current.CancellationToken));

        Assert.Equal([1, 2], harness.Invalidated.Select(e => e.ApiToken.User.LocalID).Order());
        Assert.Equal(203, Assert.Single(harness.Tokens.GetAll()).AuthID);
    }

    [Fact]
    public void ReadingAnExpiredToken_FindsNothing_AndLeavesItForTheCleanup()
    {
        var harness = new Harness();
        const string token = "44444444-4444-4444-8444-444444444444";
        harness.Tokens.Cache.Update(new() { AuthID = 301, UserID = 1, DeviceName = "web", Token = token, ExpiresAt = DateTime.Now.AddMinutes(-1) });

        Assert.Null(harness.Tokens.GetByToken(token));

        Assert.Single(harness.Tokens.GetAll());
        Assert.Empty(harness.Invalidated);
    }

    internal sealed class Harness
    {
        public readonly List<JMMUser> Users =
        [
            new() { JMMUserID = 1, Username = "alice", IsAdmin = 1 },
            new() { JMMUserID = 2, Username = "bob", IsAdmin = 1 },
        ];

        public readonly List<ApiTokenChangedEventArgs> Generated = [];

        public readonly List<ApiTokenChangedEventArgs> Invalidated = [];

        public readonly UserService Service;

        public readonly AuthTokensRepository Tokens;

        public Harness()
        {
            var nextID = 1;
            var tokens = Writable<AuthTokensRepository, AuthTokens>(t => t.AuthID);
            tokens.Setup(r => r.Save(It.IsAny<AuthTokens>())).Callback<AuthTokens>(t =>
            {
                if (t.AuthID is 0)
                    t.AuthID = nextID++;
                tokens.Object.Cache.Update(t);
            });
            Tokens = tokens.Object;

            Service = new UserService(
                NullLogger<UserService>.Instance,
                null!,
                null!,
                Writable<JMMUserRepository, JMMUser>(u => u.JMMUserID, Users).Object,
                tokens.Object,
                null!,
                Writable<AnimeGroup_UserRepository, AnimeGroup_User>(u => u.AnimeGroup_UserID).Object,
                Writable<AnimeSeries_UserRepository, AnimeSeries_User>(u => u.AnimeSeries_UserID).Object,
                Writable<AnimeEpisode_UserRepository, AnimeEpisode_User>(u => u.AnimeEpisode_UserID).Object,
                Writable<VideoLocal_UserRepository, VideoLocal_User>(u => u.VideoLocal_UserID).Object,
                new Mock<UserConfigurationRepository>(new object[] { null! }).Object,
                null!
            );
            Service.ApiTokenGenerated += (_, e) => Generated.Add(e);
            Service.ApiTokenInvalidated += (_, e) => Invalidated.Add(e);
        }

        private static Mock<TRepo> Writable<TRepo, TEntity>(Func<TEntity, int> key, IEnumerable<TEntity>? entities = null)
            where TRepo : BaseCachedRepository<TEntity, int>
            where TEntity : class, new()
        {
            var mock = CachedRepo.BuildWritable<TRepo, int, TEntity>(key, entities);
            mock.Setup(r => r.Delete(It.IsAny<IReadOnlyCollection<TEntity>>())).Callback<IReadOnlyCollection<TEntity>>(items =>
            {
                foreach (var item in items.ToList())
                    mock.Object.Cache.Remove(item);
            });
            return mock;
        }
    }
}
