using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.User.Services;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
///   An API token is stored with its creation time, keeps its ID, records
///   its use, and is revoked through the object the list hands out.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ApiTokenTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public async Task AToken_KeepsItsIDAndTimes_AndIsRevokedByTheListedObject()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var users = fixture.Services.GetRequiredService<IUserService>();
        var tokens = fixture.Services.GetRequiredService<AuthTokensRepository>();
        var user = users.GetUsers().First();
        var device = $"api-token-test-{Guid.NewGuid():N}";
        var before = DateTime.Now.AddSeconds(-1);

        var made = await users.GenerateApiTokenForUser(user, device);
        try
        {
            Assert.True(made.ID > 0);
            Assert.InRange(made.CreatedAt!.Value, before, DateTime.Now.AddSeconds(1));
            Assert.Null(made.LastUsedAt);

            var stored = tokens.GetByToken(made.Token)!;
            tokens.RecordUse(stored);
            var firstUse = stored.LastUsedAt;
            Assert.NotNull(firstUse);
            tokens.RecordUse(stored);
            Assert.Equal(firstUse, stored.LastUsedAt);

            var listed = Assert.Single(users.GetApiTokensForUser(user), token => token.ID == made.ID);
            Assert.Equal((device, made.CreatedAt, firstUse), (listed.Device, listed.CreatedAt, listed.LastUsedAt));

            Assert.True(await users.InvalidateApiToken(listed));
            Assert.DoesNotContain(users.GetApiTokensForUser(user), token => token.ID == made.ID);
        }
        finally
        {
            await users.InvalidateApiToken(made.Token);
        }
    }
}
