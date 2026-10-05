using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Stores a start season override in the migrated database and reads it back
/// from it, so the <c>AniDB_Anime_StartSeasonOverride</c> mapping, its column
/// types and its unique index are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StartSeasonOverrideRoundTripTests(DatabaseMigrationFixture fixture)
{
    #region Tests

    [Fact]
    public void AnOverrideReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<AniDB_Anime_StartSeasonOverrideRepository>();
        var store = fixture.Services.GetRequiredService<AnidbStartSeasonOverrides>();
        var createdAt = new DateTime(2026, 10, 5, 12, 30, 15, DateTimeKind.Utc);
        const int AnimeID = 987_654;

        try
        {
            store.Set(AnimeID, 2015, YearlySeason.Fall, 1, createdAt);
            store.Set(AnimeID, 2016, YearlySeason.Winter, null, createdAt.AddHours(1));
            repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

            var row = repository.GetByAnimeID(AnimeID);
            Assert.NotNull(row);
            Assert.Equal((2016, YearlySeason.Winter), (row.Year, row.Season));
            Assert.Null(row.UserID);
            Assert.Equal(createdAt, DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc));
            Assert.Equal(createdAt.AddHours(1), DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc));

            // One override per anime: a second row for it is refused by the unique index.
            var duplicate = new AniDB_Anime_StartSeasonOverride
            {
                AnimeID = AnimeID,
                Year = 2000,
                Season = YearlySeason.Spring,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };
            Assert.ThrowsAny<Exception>(() => repository.Save(duplicate));
        }
        finally
        {
            repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            store.Remove(AnimeID);
        }

        repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(repository.GetByAnimeID(AnimeID));
    }

    #endregion
}
