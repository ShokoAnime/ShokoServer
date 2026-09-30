using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the steps that copy AniDB's anime and episode titles and the core's tag names out of the
/// released tables into the text store, and checks every copied value and what the AniDB routes
/// answer afterwards against what the old tables gave.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AnidbTextMigrationTests(DatabaseMigrationFixture fixture)
{
    #region Fixture Data

    private const int AnimeID = 987_901;

    private const int EpisodeID = 987_911;

    private const int SpecialID = 987_912;

    private const int OrphanEpisodeID = 987_913;

    private const int RenamedTagID = 987_921;

    private const int TagID = 987_922;

    private static readonly int[] _ids = [AnimeID, EpisodeID, SpecialID, OrphanEpisodeID, RenamedTagID, TagID];

    /// <summary>
    /// The old anime titles, in the order they were written, with their place in AniDB's own
    /// order: two share a text, and the last one is missing from the anime's <c>AllTitles</c>.
    /// </summary>
    private static readonly (string Language, string Type, string Value, int Ordering)[] _oldAnimeTitles =
    [
        ("x-jat", "main", "Kono `Anime`", 2),
        ("EN", "official", "The Anime", 0),
        ("ja", "official", "この作品", 3),
        ("en", "synonym", "TA", 1),
        ("x-jat", "short", "TA", 4),
        ("de", "official", "Das Anime", 5),
    ];

    /// <summary>
    /// The anime's old <c>AllTitles</c> column, in the order AniDB listed its titles when it was
    /// last imported.
    /// </summary>
    private const string OldAllTitles = "The Anime|TA|Kono 'Anime'|この作品|TA";

    /// <summary>
    /// The old episode titles, in the order they were written.
    /// </summary>
    private static readonly (int EpisodeID, string Language, string Value)[] _oldEpisodeTitles =
    [
        (EpisodeID, "EN", "Episode 5"),
        (EpisodeID, "ja", "第5話"),
        (EpisodeID, "x-jat", "Dai Go Wa"),
        (EpisodeID, "FR", "Épisode 5"),
        (EpisodeID, "en", "Episode 17"),
        (SpecialID, "en", "Episode S2"),
        (SpecialID, "de", "Episode S2"),
        (SpecialID, "JA", "特別編"),
        (OrphanEpisodeID, "en", "Episode 1"),
    ];

    #endregion

    #region Helpers

    private static string IDs => string.Join(", ", _ids.Select(id => $"'{id}'"));

    private static void Cleanup(IDbConnection connection)
    {
        Execute(connection, $"DELETE FROM Metadata_Title WHERE EntitySource = {MetadataNumberRegistry.GetNumber(MetadataSource.AniDB)} AND EntityID IN ({IDs})");
    }

    /// <summary>
    /// The title the old anime model chose: the main title for <c>x-main</c>, else its main or
    /// official title in a preferred language, else any title in it when synonyms are allowed.
    /// </summary>
    private static string? OldPreferredAnimeTitle(IReadOnlyList<(TitleLanguage Language, TitleType Type, string Value)> titles)
    {
        var useSynonyms = ISettingsProvider.Instance.GetSettings().Language.UseSynonyms;
        foreach (var language in Languages.PreferredNamingLanguages.Select(language => language.Language))
        {
            if (language is TitleLanguage.Main)
                return titles.First(title => title.Type is TitleType.Main).Value;

            var inLanguage = titles.Where(title => title.Language == language).ToList();
            var found = inLanguage.FirstOrDefault(title => title.Type is TitleType.Main).Value
                ?? inLanguage.FirstOrDefault(title => title.Type is TitleType.Official).Value
                ?? (useSynonyms ? inLanguage.FirstOrDefault().Value : null);
            if (found is not null)
                return found;
        }

        return null;
    }

    #endregion

    #region Tests

    [Fact]
    public void TheCopyStepsKeepEveryValueAndEveryRouteAnswersAsBefore()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var animeRepository = services.GetRequiredService<AniDB_AnimeRepository>();
        var episodes = services.GetRequiredService<AniDB_EpisodeRepository>();
        var tags = services.GetRequiredService<AniDB_TagRepository>();
        var texts = services.GetRequiredService<TextCache>();

        var anime = new AniDB_Anime { AnimeID = AnimeID, MainTitle = "Kono 'Anime'", AnimeType = AnimeType.TVSeries, Description = "An anime.", AllTags = string.Empty };
        var episode = new AniDB_Episode { EpisodeID = EpisodeID, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode, EpisodeNumber = 5, Description = string.Empty };
        var special = new AniDB_Episode { EpisodeID = SpecialID, AnimeID = AnimeID, EpisodeType = EpisodeType.Special, EpisodeNumber = 2, Description = string.Empty };
        var renamedTag = new AniDB_Tag { TagID = RenamedTagID, TagNameSource = "new", LastUpdated = DateTime.Now };
        var tag = new AniDB_Tag { TagID = TagID, TagNameSource = "plain", LastUpdated = DateTime.Now };
        animeRepository.Save(anime);
        episodes.Save([episode, special]);
        tags.Save([renamedTag, tag]);

        using var connection = fixture.OpenConnection();
        try
        {
            // What a database upgraded from a released version holds until the drop steps run.
            ReleasedTextSchema.Restore(connection, fixture.Backend);
            Execute(connection, "UPDATE AniDB_Anime SET AllTitles = @all WHERE AnimeID = @id", ("@all", OldAllTitles), ("@id", AnimeID));
            foreach (var (language, type, value, _) in _oldAnimeTitles)
                Execute(connection, "INSERT INTO AniDB_Anime_Title (AnimeID, TitleType, Language, Title) VALUES (@id, @type, @language, @value)",
                    ("@id", AnimeID), ("@type", type), ("@language", language), ("@value", value));
            foreach (var (episodeID, language, value) in _oldEpisodeTitles)
                Execute(connection, "INSERT INTO AniDB_Episode_Title (AniDB_EpisodeID, Language, Title) VALUES (@id, @language, @value)",
                    ("@id", episodeID), ("@language", language), ("@value", value));
            Execute(connection, $"UPDATE AniDB_Tag SET TagNameOverride = 'origin`al work' WHERE TagID = {RenamedTagID}");

            // Twice, as a step interrupted once is run again from the start.
            for (var run = 0; run < 2; run++)
            {
                Run(DatabaseFixes.MigrateAnidbAnimeTitles, connection);
                Run(DatabaseFixes.MigrateAnidbEpisodeTitles, connection);
                Run(DatabaseFixes.MigrateAnidbTagOverrides, connection);
            }

            // Every copied value, exactly, the codes as the rows spelled them.
            var anidb = MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);
            var shoko = MetadataNumberRegistry.GetNumber(MetadataSource.Shoko);
            string Kind(MetadataEntityType type) => MetadataNumberRegistry.GetNumber(type).ToString();
            string Row(MetadataEntityType type, int id, byte source, string code, TitleType titleType, string value, int preference, int ordering)
                => $"{anidb}|{Kind(type)}|{id}|{source}|{code.GetTitleLanguage().GetString()}|{code}|NULL|NULL|{(int)titleType}|{value}|1|{preference}|{ordering}|NULL";
            var expected = new[]
            {
                Row(MetadataEntityType.Series, AnimeID, anidb, "x-jat", TitleType.Main, "Kono 'Anime'", 0, 2),
                Row(MetadataEntityType.Series, AnimeID, anidb, "EN", TitleType.Official, "The Anime", 0, 0),
                Row(MetadataEntityType.Series, AnimeID, anidb, "ja", TitleType.Official, "この作品", 0, 3),
                Row(MetadataEntityType.Series, AnimeID, anidb, "en", TitleType.Synonym, "TA", 0, 1),
                Row(MetadataEntityType.Series, AnimeID, anidb, "x-jat", TitleType.Short, "TA", 0, 4),
                Row(MetadataEntityType.Series, AnimeID, anidb, "de", TitleType.Official, "Das Anime", 0, 5),
                Row(MetadataEntityType.Episode, EpisodeID, anidb, "x-jat", TitleType.None, "Dai Go Wa", 0, 0),
                Row(MetadataEntityType.Episode, EpisodeID, anidb, "en", TitleType.Main, "Episode 17", 0, 1),
                Row(MetadataEntityType.Episode, SpecialID, anidb, "JA", TitleType.None, "特別編", 0, 0),
                Row(MetadataEntityType.Episode, OrphanEpisodeID, anidb, "en", TitleType.Main, "Episode 1", 0, 0),
                Row(MetadataEntityType.Tag, RenamedTagID, shoko, "en", TitleType.Main, "origin'al work", (int)TextPreference.Overall, 0),
            };
            var rows = Read(connection, $"SELECT EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, TitleType, Value, IsEnabled, Preference, Ordering, ReferenceID FROM Metadata_Title WHERE EntitySource = {anidb} AND EntityID IN ({IDs})");
            Assert.Equal(
                expected.OrderBy(row => int.Parse(row.Split('|')[1])).ThenBy(row => row.Split('|')[2], StringComparer.Ordinal).ThenBy(row => int.Parse(row.Split('|')[12])),
                rows.OrderBy(row => int.Parse(row.Split('|')[1])).ThenBy(row => row.Split('|')[2], StringComparer.Ordinal).ThenBy(row => int.Parse(row.Split('|')[12]))
            );

            // What the routes answer, as a restart would read it.
            texts.Populate(false, TestContext.Current.CancellationToken);
            animeRepository.Populate(false, TestContext.Current.CancellationToken);
            episodes.Populate(false, TestContext.Current.CancellationToken);
            tags.Populate(false, TestContext.Current.CancellationToken);
            anime = animeRepository.GetByAnimeID(AnimeID)!;
            episode = episodes.GetByEpisodeID(EpisodeID)!;
            special = episodes.GetByEpisodeID(SpecialID)!;

            // The anime: every title, in AniDB's order, with the code of its language.
            var oldTitles = _oldAnimeTitles
                .OrderBy(title => title.Ordering)
                .Select(title => (Language: title.Language.GetTitleLanguage(), Type: title.Type.GetTitleType(), Value: title.Value.Replace('`', '\'')))
                .ToList();
            var preferred = OldPreferredAnimeTitle(oldTitles) ?? oldTitles[0].Value;
            var oldAnswer = JsonConvert.SerializeObject(new
            {
                Title = preferred,
                Titles = oldTitles.Select(title => new
                {
                    Name = title.Value,
                    Language = title.Language.GetString(),
                    Type = title.Type,
                    Default = title.Value == anime.MainTitle,
                    Preferred = title.Value == preferred,
                    Source = "AniDB",
                }),
            });
            var animeDto = new AnidbAnime(anime);
            Assert.Equal(oldAnswer, JsonConvert.SerializeObject(new
            {
                animeDto.Title,
                Titles = animeDto.Titles!.Select(title => new { title.Name, title.Language, title.Type, title.Default, title.Preferred, title.Source }),
            }));
            Assert.Equal("The Anime|TA|Kono 'Anime'|この作品|TA|Das Anime", anime.AllTitles);

            // The episodes: every title but the generic ones with their own number, and the
            // first English title as the default; AniDB's generic title when there is none.
            var episodeDto = new AnidbEpisode(episode);
            Assert.Equal(["Dai Go Wa", "Episode 17"], episodeDto.Titles.Select(title => title.Name));
            Assert.Equal(["x-jat", "en"], episodeDto.Titles.Select(title => title.Language));
            Assert.Equal("Episode 17", episode.EnglishTitle);
            var specialDto = new AnidbEpisode(special);
            Assert.Equal(["特別編"], specialDto.Titles.Select(title => title.Name));
            Assert.Equal(["ja"], specialDto.Titles.Select(title => title.Language));
            Assert.Equal("Episode S2", special.EnglishTitle);
            Assert.Equal("Episode S2", specialDto.Title);

            Assert.Equal("origin'al work", tags.GetByTagID(RenamedTagID)!.TagName);
            Assert.Equal("plain", tags.GetByTagID(TagID)!.TagName);
            Assert.Contains(tags.GetByName("origin'al work"), found => found.TagID == RenamedTagID);
        }
        finally
        {
            Cleanup(connection);
            ReleasedTextSchema.Drop(fixture, connection);
            texts.Populate(false, TestContext.Current.CancellationToken);
            tags.Populate(false, TestContext.Current.CancellationToken);
            tags.Delete(tags.GetByTagID(RenamedTagID)!);
            tags.Delete(tags.GetByTagID(TagID)!);
            episodes.Delete([episode, special]);
            animeRepository.Delete(anime);
        }
    }

    #endregion
}
