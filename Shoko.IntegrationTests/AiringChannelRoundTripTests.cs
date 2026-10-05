using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories.Cached.Airing;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Stores a hidden channel in the migrated database and reads it back from
/// it, so the <c>AiringChannel.IsHidden</c> column and its mapping are checked
/// on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AiringChannelRoundTripTests(DatabaseMigrationFixture fixture)
{
    #region Tests

    [Fact]
    public void TheHiddenStateReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<AiringChannelRepository>();
        var hidden = new AiringChannel("Round Trip Hidden", AiringChannelType.Television, "JP") { IsHidden = true };
        var shown = new AiringChannel("Round Trip Shown", AiringChannelType.Television, "JP");

        try
        {
            repository.Save(hidden);
            repository.Save(shown);
            repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(repository.GetByChannelID(hidden.ChannelID)?.IsHidden);
            Assert.False(repository.GetByChannelID(shown.ChannelID)?.IsHidden);
        }
        finally
        {
            repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            foreach (var channelID in new[] { hidden.ChannelID, shown.ChannelID })
            {
                if (repository.GetByChannelID(channelID) is { } row)
                    repository.Delete(row);
            }
        }
    }

    #endregion
}
