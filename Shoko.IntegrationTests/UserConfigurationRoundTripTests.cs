using System;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Direct;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Saves a user configuration in the migrated database and reads it back
/// from it, so the <c>UserConfiguration</c> mapping, its column types and its
/// unique index are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class UserConfigurationRoundTripTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void AUsersConfigurationReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var service = fixture.Services.GetRequiredService<IUserConfigurationService>();
        var repository = fixture.Services.GetRequiredService<UserConfigurationRepository>();
        var user = new JMMUser { JMMUserID = 4242, Username = "configuration-round-trip" };

        service.Save(user, "WebUI", """{ "theme": "light", "page": { "size": 50 } }""");
        // A second save of one configuration replaces the row, which the unique index would refuse to duplicate.
        service.Save(user, "webui", """{ "theme": "light", "page": { "size": 100 } }""");

        try
        {
            Assert.Equal(100, JObject.Parse(service.Load(user, "webui")!)["page"]?["size"]?.Value<int>());
            var row = Assert.Single(repository.GetByUserID(user.JMMUserID));
            Assert.Equal("webui", row.ConfigurationID);
            Assert.NotEqual(Guid.Empty, row.PluginID);
        }
        finally
        {
            service.Delete(user, "webui");
        }

        Assert.Null(service.Load(user, "webui"));
        Assert.Empty(repository.GetByUserID(user.JMMUserID));
    }
}
