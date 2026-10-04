using System;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the steps that spell AniDB's language codes in one casing and remove the generic episode and
/// season titles carrying their entry's own number, twice, and checks which rows stay.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class GenericTitleCleanupTests(DatabaseMigrationFixture fixture)
{
    private const int AnidbEpisodeID = 9_870_401;

    private const int AnidbAnimeID = 9_870_402;

    private const int TmdbEpisodeID = 9_870_403;

    private const int TmdbSeasonID = 9_870_404;

    private const int UnknownEpisodeID = 9_870_405;

    private static readonly DateTime _updated = new(2024, 1, 1, 10, 0, 0);

    private static string Entries
        => $"'{AnidbEpisodeID}', '{AnidbAnimeID}', '{TmdbEpisodeID}', '{TmdbSeasonID}', '{UnknownEpisodeID}'";

    private static int Number(MetadataSource source)
        => MetadataNumberRegistry.GetNumber(source);

    private static int Kind(MetadataEntityType entityType)
        => MetadataNumberRegistry.GetNumber(entityType);

    private static void Title(
        IDbConnection connection,
        MetadataSource source,
        MetadataEntityType type,
        int id,
        string language,
        string code,
        string value,
        int ordering
    )
        => Insert(
            connection,
            "Metadata_Title",
            ("EntitySource", Number(source)),
            ("EntityType", Kind(type)),
            ("EntityID", id.ToString()),
            ("Source", Number(source)),
            ("Language", language),
            ("LanguageCode", code),
            ("CountryCode", null),
            ("TitleType", 0),
            ("Value", value),
            ("IsEnabled", true),
            ("Preference", 0),
            ("Ordering", ordering)
        );

    [Fact]
    public void OnlyTitlesCarryingTheirEntrysOwnNumberGoAndAniDBsCodesTakeOneCasing()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        var episodes = fixture.Services.GetRequiredService<AniDB_EpisodeRepository>();
        var anidbEpisode = new AniDB_Episode
        {
            EpisodeID = AnidbEpisodeID,
            AnimeID = AnidbAnimeID,
            EpisodeType = EpisodeType.Episode,
            EpisodeNumber = 5,
            Description = string.Empty,
        };
        episodes.Save(anidbEpisode);
        using var connection = fixture.OpenConnection();
        try
        {
            var tmdb = Number(MetadataSource.TMDB);
            Insert(
                connection,
                "Metadata_Episode",
                ("Source", tmdb),
                ("ProviderID", TmdbEpisodeID.ToString()),
                ("SeriesID", "1"),
                ("EpisodeNumber", 3),
                ("Type", (int)EpisodeType.Episode),
                ("Rating", 0),
                ("RatingVotes", 0),
                ("Runtime", 0),
                ("CreatedAt", _updated),
                ("LastUpdatedAt", _updated)
            );
            Insert(
                connection,
                "Metadata_Season",
                ("Source", tmdb),
                ("ProviderID", TmdbSeasonID.ToString()),
                ("SeriesID", "1"),
                ("SeasonNumber", 2),
                ("CreatedAt", _updated),
                ("LastUpdatedAt", _updated)
            );
            Title(connection, MetadataSource.AniDB, MetadataEntityType.Episode, AnidbEpisodeID, "en", "EN", "Episode 17", 0);
            Title(connection, MetadataSource.AniDB, MetadataEntityType.Episode, AnidbEpisodeID, "ja", "JA", "第五話", 1);
            Title(connection, MetadataSource.AniDB, MetadataEntityType.Episode, AnidbEpisodeID, "en", "EN", "Episode S5", 2);
            Title(connection, MetadataSource.AniDB, MetadataEntityType.Episode, AnidbEpisodeID, "x-jat", "X-JAT", "Hajimari", 3);
            Title(connection, MetadataSource.AniDB, MetadataEntityType.Episode, AnidbEpisodeID, "cs", "cz", "Začátek", 4);
            Title(connection, MetadataSource.AniDB, MetadataEntityType.Series, AnidbAnimeID, "pt-BR", "pt-br", "Episode 5", 0);
            Title(connection, MetadataSource.TMDB, MetadataEntityType.Episode, TmdbEpisodeID, "fr", "FR", "TBA", 0);
            Title(connection, MetadataSource.TMDB, MetadataEntityType.Episode, TmdbEpisodeID, "en-US", "EN", "To Be Announced", 1);
            Title(connection, MetadataSource.TMDB, MetadataEntityType.Episode, TmdbEpisodeID, "zh", "zh", "总第3集", 2);
            Title(connection, MetadataSource.TMDB, MetadataEntityType.Season, TmdbSeasonID, "de", "de", "Staffel 2", 0);
            Title(connection, MetadataSource.TMDB, MetadataEntityType.Season, TmdbSeasonID, "en-US", "en", "Season 11", 1);
            Title(connection, MetadataSource.TMDB, MetadataEntityType.Episode, UnknownEpisodeID, "en-US", "en", "Episode 1", 0);
            for (var run = 0; run < 2; run++)
            {
                Run(DatabaseFixes.FixAnidbTitleLanguageCodeCasing, connection);
                Run(DatabaseFixes.RemoveGenericTitles, connection);
            }

            // A form with another number or type stays, as does any form on an entry not stored.
            Assert.Equal(
                [
                    $"{AnidbEpisodeID}|en|Episode 17", $"{AnidbEpisodeID}|en|Episode S5", $"{AnidbEpisodeID}|x-jat|Hajimari", $"{AnidbEpisodeID}|cz|Začátek",
                    $"{AnidbAnimeID}|pt-BR|Episode 5",
                    $"{TmdbEpisodeID}|EN|To Be Announced",
                    $"{TmdbSeasonID}|en|Season 11",
                    $"{UnknownEpisodeID}|en|Episode 1",
                ],
                Read(connection, $"SELECT EntityID, LanguageCode, Value FROM Metadata_Title WHERE EntityID IN ({Entries}) ORDER BY EntityID, Ordering")
            );
        }
        finally
        {
            Execute(connection, $"DELETE FROM Metadata_Title WHERE EntityID IN ({Entries})");
            Execute(connection, $"DELETE FROM Metadata_Episode WHERE ProviderID = '{TmdbEpisodeID}'");
            Execute(connection, $"DELETE FROM Metadata_Season WHERE ProviderID = '{TmdbSeasonID}'");
            episodes.Delete(anidbEpisode);
        }
    }
}
