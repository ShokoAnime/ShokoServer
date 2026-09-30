using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the steps that copy TMDB's titles, overviews and people's other names
/// out of the released tables into the text store, and checks every copied
/// value and what the TMDB routes answer afterwards against what the old
/// tables gave.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TmdbTextMigrationTests(DatabaseMigrationFixture fixture)
{
    #region Fixture Data

    private const int ShowID = 987_801;

    private const int RenamedShowID = 987_802;

    private const int EpisodeID = 987_811;

    private const int UnlistedEpisodeID = 987_812;

    private const int SeasonID = 987_831;

    private const int MovieID = 987_821;

    private const int CollectionID = 987_841;

    private const int PersonID = 987_851;

    private const int NamelessPersonID = 987_852;

    private static readonly int[] _ids = [ShowID, RenamedShowID, EpisodeID, UnlistedEpisodeID, SeasonID, MovieID, CollectionID, PersonID, NamelessPersonID];

    /// <summary>
    /// A row of the old title or overview table.
    /// </summary>
    private sealed record OldRow(ForeignEntityType Type, int ID, string Language, string Country, string Value);

    /// <summary>
    /// The old titles, in the order they were written.
    /// </summary>
    private static readonly OldRow[] _oldTitles =
    [
        new(ForeignEntityType.Show, ShowID, "ja", "JP", "番組"),
        new(ForeignEntityType.Show, ShowID, "en", "US", "The Show"),
        new(ForeignEntityType.Show, ShowID, "fr", "FR", "Le Show"),
        new(ForeignEntityType.Show, ShowID, "en", "GB", "The Show UK"),
        new(ForeignEntityType.Show, RenamedShowID, "de", "DE", "Die Show"),
        new(ForeignEntityType.Show, RenamedShowID, "en", "US", "Old Name"),
        new(ForeignEntityType.Episode, EpisodeID, "en", "US", "Episode 5"),
        new(ForeignEntityType.Episode, EpisodeID, "ja", "JP", "第5話"),
        new(ForeignEntityType.Episode, EpisodeID, "ko", "KR", "진짜 제목"),
        new(ForeignEntityType.Episode, EpisodeID, "zh", "CN", "第7集"),
        new(ForeignEntityType.Episode, UnlistedEpisodeID, "ja", "JP", "本当"),
        new(ForeignEntityType.Episode, UnlistedEpisodeID, "fr", "", "Titre"),
        new(ForeignEntityType.Season, SeasonID, "en", "US", "Season 1"),
        new(ForeignEntityType.Movie, MovieID, "fr", "FR", "Le Film"),
        new(ForeignEntityType.Movie, MovieID, "ja", "JP", "映画"),
        new(ForeignEntityType.Movie, MovieID, "en", "US", "The Film"),
        new(ForeignEntityType.Collection, CollectionID, "de", "DE", "Die Sammlung"),
        new(ForeignEntityType.Collection, CollectionID, "en", "US", "The Collection"),
    ];

    /// <summary>
    /// The old overviews, in the order they were written.
    /// </summary>
    private static readonly OldRow[] _oldOverviews =
    [
        new(ForeignEntityType.Show, ShowID, "en", "US", "An overview."),
        new(ForeignEntityType.Show, ShowID, "de", "DE", "Eine Übersicht."),
        new(ForeignEntityType.Show, RenamedShowID, "ja", "JP", "概要"),
        new(ForeignEntityType.Episode, EpisodeID, "fr", "FR", "Un résumé."),
        new(ForeignEntityType.Episode, EpisodeID, "en", "US", "What happens."),
        new(ForeignEntityType.Episode, UnlistedEpisodeID, "en", "GB", "British words."),
        new(ForeignEntityType.Movie, MovieID, "en", "US", "Stale text."),
        new(ForeignEntityType.Collection, CollectionID, "en", "US", "All of them."),
        new(ForeignEntityType.Person, PersonID, "ja", "JP", "経歴"),
        new(ForeignEntityType.Person, PersonID, "en", "US", "A life."),
    ];

    #endregion

    #region Helpers

    private static List<object?[]> Read(IDbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var values = new object?[reader.FieldCount];
            for (var index = 0; index < values.Length; index++)
                values[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            rows.Add(values);
        }

        return rows;
    }

    private void InsertOld(IDbConnection connection, string table, IEnumerable<OldRow> rows)
    {
        foreach (var row in rows)
            Execute(
                connection,
                $"INSERT INTO {table} (ParentID, ParentType, LanguageCode, CountryCode, Value) VALUES (@id, @type, @language, @country, @value)",
                ("@id", row.ID),
                ("@type", (int)row.Type),
                ("@language", row.Language),
                ("@country", row.Country),
                ("@value", row.Value)
            );
    }

    private void Cleanup(IDbConnection connection)
    {
        var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
        foreach (var id in _ids)
        {
            Execute(connection, $"DELETE FROM Metadata_Title WHERE EntitySource = {tmdb} AND EntityID = '{id}'");
            Execute(connection, $"DELETE FROM Metadata_Overview WHERE EntitySource = {tmdb} AND EntityID = '{id}'");
        }
    }

    /// <summary>
    /// A title as the old table handed it out.
    /// </summary>
    private static ITitle OldTitle(OldRow row)
        => new TitleStub
        {
            Source = MetadataSource.TMDB,
            Language = string.IsNullOrEmpty(row.Country) ? row.Language.GetTitleLanguage() : row.Language.GetTitleLanguage(row.Country),
            LanguageCode = row.Language,
            CountryCode = row.Country,
            Value = row.Value,
            Type = TitleType.Official,
        };

    /// <summary>
    /// An overview as the old table handed it out.
    /// </summary>
    private static IText OldOverview(OldRow row)
        => new TextStub
        {
            Source = MetadataSource.TMDB,
            Language = string.IsNullOrEmpty(row.Country) ? row.Language.GetTitleLanguage() : row.Language.GetTitleLanguage(row.Country),
            LanguageCode = row.Language,
            CountryCode = row.Country,
            Value = row.Value,
        };

    /// <summary>
    /// The title the old models chose: the English one for <c>x-main</c>, else the first one in a
    /// preferred language, else the English one.
    /// </summary>
    private static ITitle OldPreferredTitle(IReadOnlyList<ITitle> titles, string english, bool episodeLanguages)
    {
        var fallback = OldTitle(new(ForeignEntityType.None, 0, "en", "US", english));
        foreach (var language in episodeLanguages ? Languages.PreferredEpisodeNamingLanguages : Languages.PreferredNamingLanguages)
        {
            if (language.Language is TitleLanguage.Main)
                return fallback;
            if (titles.GetByLanguage(language.Language) is { } title)
                return title;
        }

        return fallback;
    }

    /// <summary>
    /// The overview the old models chose: the first one in a preferred language, else the English one.
    /// </summary>
    private static IText OldPreferredOverview(IReadOnlyList<IText> overviews, string english)
    {
        foreach (var language in Languages.PreferredDescriptionNamingLanguages)
            if (overviews.GetByLanguage(language.Language) is { } overview)
                return overview;

        return OldOverview(new(ForeignEntityType.None, 0, "en", "US", english));
    }

    /// <summary>
    /// What a TMDB route answered for an entry's text before the move.
    /// </summary>
    private static string OldAnswer(ForeignEntityType type, int id, string englishTitle, string englishOverview, bool episodeLanguages, int? episodeNumber = null)
    {
        var titles = _oldTitles.Where(row => row.Type == type && row.ID == id)
            .Select(OldTitle)
            .ToList();
        var overviews = _oldOverviews.Where(row => row.Type == type && row.ID == id).Select(OldOverview).ToList();
        var preferredTitle = OldPreferredTitle(titles, englishTitle, episodeLanguages);
        var preferredOverview = OldPreferredOverview(overviews, englishOverview);

        // Decided change: a generic episode title with the episode's own number is no longer
        // listed, unless it is the English title on the row.
        if (episodeNumber is { } number)
            titles = [.. titles.Where(title => (title.LanguageCode == "en" && title.CountryCode == "US" && title.Value == englishTitle) || !GenericEpisodeTitles.IsGeneric(title.Value, EpisodeType.Episode, number))];

        return JsonConvert.SerializeObject(new
        {
            Title = preferredTitle.Value,
            Titles = titles.ToTitleDto(englishTitle, preferredTitle),
            Overview = preferredOverview.Value,
            Overviews = overviews.ToOverviewDto(englishOverview, preferredOverview),
        });
    }

    private static string NewAnswer(string title, object titles, string overview, object overviews)
        => JsonConvert.SerializeObject(new { Title = title, Titles = titles, Overview = overview, Overviews = overviews });

    #endregion

    #region Tests

    [Fact]
    public void TheCopyStepsKeepEveryValueAndEveryRouteAnswersAsBefore()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var shows = services.GetRequiredService<TMDB_ShowRepository>();
        var seasons = services.GetRequiredService<TMDB_SeasonRepository>();
        var episodes = services.GetRequiredService<TMDB_EpisodeRepository>();
        var movies = services.GetRequiredService<TMDB_MovieRepository>();
        var collections = services.GetRequiredService<TMDB_CollectionRepository>();
        var people = services.GetRequiredService<TMDB_PersonRepository>();
        var texts = services.GetRequiredService<TextCache>();

        var show = new TMDB_Show(ShowID) { EnglishTitle = "The Show", EnglishOverview = "An overview.", OriginalTitle = "番組", OriginalLanguageCode = "ja" };
        var renamed = new TMDB_Show(RenamedShowID) { EnglishTitle = "New Name", EnglishOverview = "Its overview." };
        var episode = new TMDB_Episode(EpisodeID) { TmdbShowID = ShowID, EpisodeNumber = 5, SeasonNumber = 1, EnglishTitle = "Episode 5", EnglishOverview = "What happens." };
        var unlisted = new TMDB_Episode(UnlistedEpisodeID) { TmdbShowID = ShowID, EpisodeNumber = 6, SeasonNumber = 1, EnglishTitle = "Real Title", EnglishOverview = "Unlisted overview." };
        var season = new TMDB_Season(SeasonID) { TmdbShowID = ShowID, SeasonNumber = 1, EnglishTitle = "Season 1", EnglishOverview = string.Empty };
        var movie = new TMDB_Movie(MovieID) { EnglishTitle = "The Film", EnglishOverview = "Fresh text." };
        var collection = new TMDB_Collection(CollectionID) { EnglishTitle = "The Collection", EnglishOverview = "All of them." };
        var person = new TMDB_Person(PersonID) { EnglishName = "Some Person", EnglishBiography = "A life." };
        var nameless = new TMDB_Person(NamelessPersonID) { EnglishName = "No Aliases", EnglishBiography = string.Empty };
        shows.Save([show, renamed]);
        episodes.Save([episode, unlisted]);
        seasons.Save(season);
        movies.Save(movie);
        collections.Save(collection);
        people.Save(person);
        people.Save(nameless);

        using var connection = fixture.OpenConnection();
        try
        {
            // What a database upgraded from a released version holds until the drop steps run.
            ReleasedTextSchema.Restore(connection, fixture.Backend);
            InsertOld(connection, "TMDB_Title", _oldTitles);
            InsertOld(connection, "TMDB_Overview", _oldOverviews);
            Execute(connection, "UPDATE TMDB_Person SET Aliases = @aliases WHERE TmdbPersonID = @id", ("@aliases", "First Alias||||||Second/Alias"), ("@id", PersonID));
            Execute(connection, "UPDATE TMDB_Person SET Aliases = @aliases WHERE TmdbPersonID = @id", ("@aliases", string.Empty), ("@id", NamelessPersonID));

            // Twice, as a step interrupted once is run again from the start.
            for (var run = 0; run < 2; run++)
            {
                Run(DatabaseFixes.MigrateTmdbTitles, connection);
                Run(DatabaseFixes.MigrateTmdbOverviews, connection);
                Run(DatabaseFixes.MigrateTmdbPersonAliases, connection);
            }

            // Every copied value, exactly.
            var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
            string Kind(MetadataEntityType type) => MetadataNumberRegistry.GetNumber(type).ToString();
            var titleRows = Read(connection, $"SELECT EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, TitleType, Value, IsEnabled, Preference, Ordering, ReferenceID FROM Metadata_Title WHERE EntitySource = {tmdb} AND EntityID IN ({string.Join(", ", _ids.Select(id => $"'{id}'"))}) ORDER BY EntityType, EntityID, Ordering")
                .Select(row => string.Join("|", row.Select(value => value is null ? "NULL" : value is bool flag ? (flag ? "1" : "0") : Convert.ToString(value))))
                .ToList();
            string Row(MetadataEntityType type, int id, string language, string code, string? country, TitleType titleType, string value, int ordering)
                => $"{tmdb}|{Kind(type)}|{id}|{tmdb}|{language}|{code}|{country ?? "NULL"}|NULL|{(int)titleType}|{value}|1|0|{ordering}|NULL";
            var expectedTitles = new[]
            {
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, ShowID, "ja", "ja", "JP", TitleType.Official, "番組", 0)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, ShowID, "fr-FR", "fr", "FR", TitleType.Official, "Le Show", 2)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, ShowID, "en-GB", "en", "GB", TitleType.Official, "The Show UK", 3)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, RenamedShowID, "de", "de", "DE", TitleType.Official, "Die Show", 0)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, RenamedShowID, "en-US", "en", "US", TitleType.Official, "Old Name", 1)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, EpisodeID, "ko", "ko", "KR", TitleType.Official, "진짜 제목", 1)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, EpisodeID, "zh-Hans", "zh", "CN", TitleType.Official, "第7集", 2)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, UnlistedEpisodeID, "ja", "ja", "JP", TitleType.Official, "本当", 0)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, UnlistedEpisodeID, "fr", "fr", null, TitleType.Official, "Titre", 1)),
                (MetadataEntityType.Movie, Row(MetadataEntityType.Movie, MovieID, "fr-FR", "fr", "FR", TitleType.Official, "Le Film", 0)),
                (MetadataEntityType.Movie, Row(MetadataEntityType.Movie, MovieID, "ja", "ja", "JP", TitleType.Official, "映画", 1)),
                (MetadataEntityType.Collection, Row(MetadataEntityType.Collection, CollectionID, "de", "de", "DE", TitleType.Official, "Die Sammlung", 0)),
                (MetadataEntityType.Creator, Row(MetadataEntityType.Creator, PersonID, "unk", "unk", null, TitleType.Synonym, "First Alias", 0)),
                (MetadataEntityType.Creator, Row(MetadataEntityType.Creator, PersonID, "unk", "unk", null, TitleType.Synonym, "Second/Alias", 1)),
            };
            Assert.Equal(
                expectedTitles.OrderBy(row => MetadataNumberRegistry.GetNumber(row.Item1)).ThenBy(row => row.Item2.Split('|')[2], StringComparer.Ordinal).ThenBy(row => int.Parse(row.Item2.Split('|')[12])).Select(row => LanguageAgnostic(row.Item2)),
                titleRows.Select(LanguageAgnostic)
            );

            var overviewRows = Read(connection, $"SELECT EntityType, EntityID, LanguageCode, CountryCode, Value, Ordering FROM Metadata_Overview WHERE EntitySource = {tmdb} AND EntityID IN ({string.Join(", ", _ids.Select(id => $"'{id}'"))}) ORDER BY EntityType, EntityID, Ordering")
                .Select(row => string.Join("|", row.Select(value => value is null ? "NULL" : Convert.ToString(value))))
                .ToList();
            var expectedOverviews = new[]
            {
                (MetadataEntityType.Series, $"{Kind(MetadataEntityType.Series)}|{ShowID}|de|DE|Eine Übersicht.|1"),
                (MetadataEntityType.Series, $"{Kind(MetadataEntityType.Series)}|{RenamedShowID}|ja|JP|概要|0"),
                (MetadataEntityType.Episode, $"{Kind(MetadataEntityType.Episode)}|{EpisodeID}|fr|FR|Un résumé.|0"),
                (MetadataEntityType.Episode, $"{Kind(MetadataEntityType.Episode)}|{UnlistedEpisodeID}|en|GB|British words.|0"),
                (MetadataEntityType.Movie, $"{Kind(MetadataEntityType.Movie)}|{MovieID}|en|US|Stale text.|0"),
                (MetadataEntityType.Creator, $"{Kind(MetadataEntityType.Creator)}|{PersonID}|ja|JP|経歴|0"),
            };
            Assert.Equal(
                expectedOverviews.OrderBy(row => MetadataNumberRegistry.GetNumber(row.Item1)).ThenBy(row => row.Item2.Split('|')[1], StringComparer.Ordinal).Select(row => row.Item2),
                overviewRows
            );

            // The flags say which entries listed their English text.
            string Flags(string table, string idColumn, int id, bool hasTitle)
                => string.Join("|", Read(connection, $"SELECT {(hasTitle ? "EnglishTitleListed, " : string.Empty)}EnglishOverviewListed FROM {table} WHERE {idColumn} = {id}").Single().Select(value => Convert.ToInt32(value)));
            Assert.Equal("1|1", Flags("TMDB_Show", "TmdbShowID", ShowID, true));
            Assert.Equal("0|0", Flags("TMDB_Show", "TmdbShowID", RenamedShowID, true));
            Assert.Equal("1|0", Flags("TMDB_Season", "TmdbSeasonID", SeasonID, true));

            // What the routes answer, as a restart would read it.
            texts.Populate(false, TestContext.Current.CancellationToken);
            shows.Populate(false, TestContext.Current.CancellationToken);
            episodes.Populate(false, TestContext.Current.CancellationToken);
            seasons.Populate(false, TestContext.Current.CancellationToken);
            movies.Populate(false, TestContext.Current.CancellationToken);
            collections.Populate(false, TestContext.Current.CancellationToken);
            show = shows.GetByTmdbShowID(ShowID)!;
            renamed = shows.GetByTmdbShowID(RenamedShowID)!;
            episode = episodes.GetByTmdbEpisodeID(EpisodeID)!;
            unlisted = episodes.GetByTmdbEpisodeID(UnlistedEpisodeID)!;
            season = seasons.GetByTmdbSeasonID(SeasonID)!;
            movie = movies.GetByTmdbMovieID(MovieID)!;
            collection = collections.GetByTmdbCollectionID(CollectionID)!;

            var showDto = new TmdbShow(show, TmdbShow.IncludeDetails.Titles | TmdbShow.IncludeDetails.Overviews);
            Assert.Equal(OldAnswer(ForeignEntityType.Show, ShowID, "The Show", "An overview.", false), NewAnswer(showDto.Title, showDto.Titles!, showDto.Overview, showDto.Overviews!));
            var renamedDto = new TmdbShow(renamed, TmdbShow.IncludeDetails.Titles | TmdbShow.IncludeDetails.Overviews);
            Assert.Equal(OldAnswer(ForeignEntityType.Show, RenamedShowID, "New Name", "Its overview.", false), NewAnswer(renamedDto.Title, renamedDto.Titles!, renamedDto.Overview, renamedDto.Overviews!));
            var episodeDto = new TmdbEpisode(show, episode, TmdbEpisode.IncludeDetails.Titles | TmdbEpisode.IncludeDetails.Overviews);
            Assert.Equal(OldAnswer(ForeignEntityType.Episode, EpisodeID, "Episode 5", "What happens.", true, 5), NewAnswer(episodeDto.Title, episodeDto.Titles!, episodeDto.Overview, episodeDto.Overviews!));
            var unlistedDto = new TmdbEpisode(show, unlisted, TmdbEpisode.IncludeDetails.Titles | TmdbEpisode.IncludeDetails.Overviews);
            Assert.Equal(OldAnswer(ForeignEntityType.Episode, UnlistedEpisodeID, "Real Title", "Unlisted overview.", true, 6), NewAnswer(unlistedDto.Title, unlistedDto.Titles!, unlistedDto.Overview, unlistedDto.Overviews!));
            var seasonDto = new TmdbSeason(season, TmdbSeason.IncludeDetails.Titles | TmdbSeason.IncludeDetails.Overviews);
            Assert.Equal(OldAnswer(ForeignEntityType.Season, SeasonID, "Season 1", string.Empty, false), NewAnswer(seasonDto.Title, seasonDto.Titles!, seasonDto.Overview, seasonDto.Overviews!));
            var movieDto = new TmdbMovie(movie, TmdbMovie.IncludeDetails.Titles | TmdbMovie.IncludeDetails.Overviews);
            Assert.Equal(OldAnswer(ForeignEntityType.Movie, MovieID, "The Film", "Fresh text.", false), NewAnswer(movieDto.Title, movieDto.Titles!, movieDto.Overview, movieDto.Overviews!));
            var collectionDto = new TmdbMovie.Collection(collection, TmdbMovie.Collection.IncludeDetails.Titles | TmdbMovie.Collection.IncludeDetails.Overviews);
            Assert.Equal(OldAnswer(ForeignEntityType.Collection, CollectionID, "The Collection", "All of them.", true), NewAnswer(collectionDto.Title, collectionDto.Titles!, collectionDto.Overview, collectionDto.Overviews!));

            // People: the biographies in their old order, and the other names in theirs.
            var reloaded = people.GetByTmdbPersonID(PersonID)!;
            Assert.Equal(["経歴", "A life."], ((IWithOverviews)reloaded).Overviews.Select(overview => overview.Value));
            Assert.Equal(["First Alias", "Second/Alias"], ((ICreator)reloaded).AlternativeNames.Select(name => name.Value));
            Assert.Empty(((ICreator)people.GetByTmdbPersonID(NamelessPersonID)!).AlternativeNames);
        }
        finally
        {
            Cleanup(connection);
            ReleasedTextSchema.Drop(fixture, connection);
            texts.Populate(false, TestContext.Current.CancellationToken);
            shows.Delete([show, renamed]);
            episodes.Delete([episode, unlisted]);
            seasons.Delete(season);
            movies.Delete(movie);
            collections.Delete(collection);
            people.Delete(person);
            people.Delete(nameless);
        }
    }

    /// <summary>
    /// A copied row without its stored language, which the loop after it checks on its own.
    /// </summary>
    private static string LanguageAgnostic(string row)
    {
        var parts = row.Split('|');
        parts[4] = "*";
        return string.Join("|", parts);
    }

    #endregion
}
