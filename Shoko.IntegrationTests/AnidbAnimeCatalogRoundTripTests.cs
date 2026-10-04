using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Xunit;

using AniDBExtensions = Shoko.Server.Providers.AniDB.AniDBExtensions;
using SeasonRules = Shoko.Server.Utilities.SeasonCalendar;

namespace Shoko.IntegrationTests;

/// <summary>
/// Lists cached AniDB anime through the started server's services, and reads
/// the studio roles of many anime in one go across the chunk size of the
/// query, so the role filter and the ID list are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AnidbAnimeCatalogRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const int FirstAnimeID = 990_401;

    private const int LastAnimeID = 990_402;

    [Fact]
    public void OnlyTheStudioRolesOfTheAskedAnimeAreRead()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var staff = fixture.Services.GetRequiredService<AniDB_Anime_StaffRepository>();
        Clean(staff, null, null);
        try
        {
            staff.Save(
            [
                Role(FirstAnimeID, 1, CreatorRoleType.Studio),
                Role(FirstAnimeID, 2, CreatorRoleType.Director),
                Role(LastAnimeID, 3, CreatorRoleType.Studio),
            ]);

            // More IDs than one chunk holds, the asked anime at both ends.
            var asked = Enumerable.Range(FirstAnimeID - 600, 600).Append(FirstAnimeID).Append(LastAnimeID).ToList();
            var roles = staff.GetStudiosByAnimeIDs(asked);

            Assert.Equal([(FirstAnimeID, 1), (LastAnimeID, 3)], roles.Select(role => (role.AnimeID, role.CreatorID)).Order());
        }
        finally
        {
            Clean(staff, null, null);
        }
    }

    [Fact]
    public void TheCachedAnimeListResolvesAndFiltersBySeason()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var staff = services.GetRequiredService<AniDB_Anime_StaffRepository>();
        var anime = services.GetRequiredService<AniDB_AnimeRepository>();
        var episodes = services.GetRequiredService<AniDB_EpisodeRepository>();
        Clean(staff, anime, episodes);
        try
        {
            var next = SeasonRules.GetNextYearlySeason(SeasonRules.GetYearlySeason(DateTime.Today.ToDateOnly()));
            var nextDay = new DateOnly(next.Year, 2 + 3 * (int)next.Season, 1);
            anime.Save([Anime(FirstAnimeID, new(2015, 4, 10)), Anime(LastAnimeID, nextDay)]);
            episodes.Save([Episode(FirstAnimeID, new(2015, 4, 10)), Episode(LastAnimeID, nextDay)]);

            var anidbService = services.GetRequiredService<IAnidbService>();
            Assert.Equal([FirstAnimeID], anidbService.GetCachedAnime(new() { Seasons = [(2015, YearlySeason.Spring)] }).Select(entry => entry.AnidbID).Where(IsOurs));
            Assert.Equal([LastAnimeID], anidbService.GetCachedAnime(new() { Seasons = [next] }).Select(entry => entry.AnidbID).Where(IsOurs));
            Assert.Contains(anidbService.GetCachedAnimeSeasons(), season => season is { Count: > 0 } && (season.Year, season.Season) == next);

            var catalog = services.GetRequiredService<AnidbAnimeCatalog>();
            Assert.Equal("Anime 990401", catalog.GetTitle(anime.GetByAnimeID(FirstAnimeID)!, null));
        }
        finally
        {
            Clean(staff, anime, episodes);
        }
    }

    [Fact]
    public void AMissingEpisodeAirDateIsStoredApartFromThePlaceholder()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var staff = fixture.Services.GetRequiredService<AniDB_Anime_StaffRepository>();
        var episodes = fixture.Services.GetRequiredService<AniDB_EpisodeRepository>();
        Clean(staff, null, episodes);
        try
        {
            var undated = Episode(FirstAnimeID, null);
            var placeholder = Episode(LastAnimeID, new(1970, 1, 1));
            episodes.Save([undated, placeholder]);

            using var session = fixture.Services.GetRequiredService<DatabaseFactory>().SessionFactory.OpenStatelessSession();
            Assert.Null(session.Get<AniDB_Episode>(undated.AniDB_EpisodeID).AirDate);
            Assert.Equal(0, session.Get<AniDB_Episode>(placeholder.AniDB_EpisodeID).AirDate);
        }
        finally
        {
            Clean(staff, null, episodes);
        }
    }

    private static bool IsOurs(int animeID)
        => animeID is FirstAnimeID or LastAnimeID;

#pragma warning disable CS0618
    private static AniDB_Anime Anime(int animeID, DateOnly airDate)
        => new()
        {
            AnimeID = animeID,
            MainTitle = $"Anime {animeID}",
            AnimeType = AnimeType.TVSeries,
            AirDate = new(airDate),
            Description = string.Empty,
            AllTags = string.Empty,
            CreatedAt = DateTime.Now,
            DateTimeUpdated = DateTime.Now,
            DateTimeDescUpdated = DateTime.Now,
        };
#pragma warning restore CS0618

    private static AniDB_Episode Episode(int animeID, DateOnly? airDate)
        => new()
        {
            EpisodeID = animeID * 10 + 1,
            AnimeID = animeID,
            EpisodeNumber = 1,
            EpisodeType = EpisodeType.Episode,
            AirDate = AniDBExtensions.GetAniDBAirDateAsSeconds(airDate?.ToDateTime(TimeOnly.MinValue)),
            CreatedAt = DateTime.Now,
            DateTimeUpdated = DateTime.Now,
        };

    private static AniDB_Anime_Staff Role(int animeID, int creatorID, CreatorRoleType roleType)
        => new()
        {
            AnimeID = animeID,
            CreatorID = creatorID,
            RoleType = roleType,
            Role = roleType is CreatorRoleType.Studio ? "Animation Work" : "Direction",
        };

    private static void Clean(AniDB_Anime_StaffRepository staff, AniDB_AnimeRepository? anime, AniDB_EpisodeRepository? episodes)
    {
        foreach (var animeID in new[] { FirstAnimeID, LastAnimeID })
        {
            staff.Delete(staff.GetByAnimeID(animeID));
            episodes?.Delete(episodes.GetByAnimeID(animeID));
            if (anime?.GetByAnimeID(animeID) is { } row)
                anime.Delete(row);
        }
    }
}
