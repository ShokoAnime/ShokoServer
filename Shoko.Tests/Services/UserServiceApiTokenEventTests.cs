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
