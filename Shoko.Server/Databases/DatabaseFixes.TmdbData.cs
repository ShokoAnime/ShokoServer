using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Services;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region TMDB Data | Constants

    /// <summary>
    ///   How many parameters one insert takes at most, so it stays under SQL
    ///   Server's limit of 2,100 per statement.
    /// </summary>
    private const int TmdbParametersPerInsert = 2000;

    /// <summary>
    ///   How many entries one page of a copy reads, plans and writes, so a
    ///   large library never holds all of its rows in memory at once.
    /// </summary>
    private const int TmdbKeysPerPage = 2000;

    /// <summary>
    ///   How many IDs one <c>IN</c> list holds.
    /// </summary>
    private const int TmdbIDsPerList = 1000;

    /// <summary>
    ///   The start of a TMDB genre's provisional tag ID, which the source's
    ///   next refresh replaces with one keyed by TMDB's own number.
    /// </summary>
    private const string TmdbGenrePrefix = "genre/";

    /// <summary>
    ///   The start of a TMDB keyword's provisional tag ID.
    /// </summary>
    private const string TmdbKeywordPrefix = "keyword/";

    /// <summary>
    ///   The start of the default ordering ID TMDB's shows had while TMDB was
    ///   one of the core's own sources.
    /// </summary>
    private const string TmdbCoreOrderingPrefix = "tmdb://ordering/";

    /// <summary>
    ///   The notes on a guest star's credit.
    /// </summary>
    private const string TmdbGuestStarNotes = "Guest star";

    #endregion

    #region TMDB Data | Entries

    /// <summary>
    ///   Copies TMDB's shows from <c>TMDB_Show</c> into <c>Metadata_Series</c>,
    ///   with the other IDs TMDB gave them as cross-source IDs, their production countries
    ///   and the time each was last refreshed.
    /// </summary>
    /// <remarks>
    ///   A show never updated since it was added was never refreshed. A
    ///   preferred ordering naming the show's default one is rewritten to the
    ///   default ordering ID of a source outside the core. Runs in one
    ///   transaction, and first removes what an earlier run of it wrote.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbShows(object connection)
        => RunTmdbDataCopy(connection, "shows", "Metadata_Series", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Series WHERE Source = {TmdbNumber}");
            var seen = new HashSet<int>();
            var rows = new List<object?[]>();
            var columns = "TmdbShowID, OriginalLanguageCode, IsRestricted, UserRating, UserVotes, FirstAiredAt, LastAiredAt, CreatedAt, LastUpdatedAt, TvdbShowID, ProductionCountries, PreferredOrderingID";
            foreach (var row in Read(transaction, $"SELECT {columns} FROM TMDB_Show ORDER BY TMDB_ShowID"))
            {
                var showID = AsInt(row[0]);
                if (showID <= 0 || !seen.Add(showID))
                    continue;

                var crossSourceIDs = AsInt(row[9]) is > 0 and var tvdbID ? new List<MetadataGuid> { CrossSource("tvdb", MetadataEntityType.Series, tvdbID.ToString(CultureInfo.InvariantCulture)) } : [];
                rows.Add([
                    TmdbNumber,
                    showID.ToString(CultureInfo.InvariantCulture),
                    (int)AnimeType.TV,
                    AsDate(row[5]) is { } aired ? new PartialDateOnly(aired).ToString() : null,
                    AsDate(row[6]) is { } ended ? new PartialDateOnly(ended).ToString() : null,
                    Math.Round(AsDouble(row[3]), 2),
                    AsInt(row[4]),
                    AsBool(row[2]) ? 1 : 0,
                    (int)ReleaseStatus.Unknown,
                    (int)SourceMaterial.Unknown,
                    NullIfEmpty(row[1]),
                    ToJson(crossSourceIDs),
                    row[8],
                    RewriteTmdbCoreOrdering(row[11] as string),
                    ToExtraJson(new Metadata_SeriesExtra { ProductionCountries = CountryCodes(row[10]) }.NullIfEmpty()),
                    row[7],
                    RefreshedAt(row[7], row[8]),
                ]);
            }

            return TmdbInsert(transaction, "Metadata_Series",
                ["Source", "ProviderID", "Type", "AirDate", "EndDate", "Rating", "RatingVotes", "IsRestricted", "ReleaseStatus", "SourceMaterial", "OriginalLanguageCode",
                    "CrossSourceIDs", "LastUpdatedAt", "PreferredOrderingID", "ExtraData", "CreatedAt", "LastRefreshedAt"],
                rows);
        });

    /// <summary>
    ///   Copies TMDB's seasons from <c>TMDB_Season</c> into
    ///   <c>Metadata_Season</c>, leaving out those of no stored show.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbSeasons(object connection)
        => RunTmdbDataCopy(connection, "seasons", "Metadata_Season", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Season WHERE Source = {TmdbNumber}");
            var shows = ReadIDs(transaction, "SELECT TmdbShowID FROM TMDB_Show").ToHashSet();
            var seen = new HashSet<int>();
            var rows = new List<object?[]>();
            foreach (var row in Read(transaction, "SELECT TmdbSeasonID, TmdbShowID, SeasonNumber, CreatedAt, LastUpdatedAt FROM TMDB_Season ORDER BY TMDB_SeasonID"))
            {
                var seasonID = AsInt(row[0]);
                if (seasonID <= 0 || !shows.Contains(AsInt(row[1])) || !seen.Add(seasonID))
                    continue;

                rows.Add([TmdbNumber, Text(seasonID), Text(AsInt(row[1])), AsInt(row[2]), row[4], row[3]]);
            }

            return TmdbInsert(transaction, "Metadata_Season", ["Source", "ProviderID", "SeriesID", "SeasonNumber", "LastUpdatedAt", "CreatedAt"], rows);
        });

    /// <summary>
    ///   Copies TMDB's episodes from <c>TMDB_Episode</c> into
    ///   <c>Metadata_Episode</c>, with their other IDs and hidden flags,
    ///   leaving out those of no stored show.
    /// </summary>
    /// <remarks>
    ///   An episode in season 0 is a special. TMDB gives the day an episode
    ///   aired but not the time, so no time is stored.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbEpisodes(object connection)
        => RunTmdbDataCopy(connection, "episodes", "Metadata_Episode", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Episode WHERE Source = {TmdbNumber}");
            var shows = ReadIDs(transaction, "SELECT TmdbShowID FROM TMDB_Show").ToHashSet();
            var seen = new HashSet<int>();
            var copied = 0;
            var columns = "TmdbEpisodeID, TmdbShowID, TmdbSeasonID, SeasonNumber, EpisodeNumber, Runtime, UserRating, UserVotes, AiredAt, CreatedAt, LastUpdatedAt, TvdbEpisodeID, IsHidden";
            foreach (var page in ReadTmdbPages(transaction, "TMDB_Episode", "TmdbEpisodeID", columns, "TmdbEpisodeID, TMDB_EpisodeID", report, "Copying TMDB episodes"))
            {
                var rows = new List<object?[]>();
                foreach (var row in page)
                {
                    var episodeID = AsInt(row[0]);
                    if (episodeID <= 0 || !shows.Contains(AsInt(row[1])) || !seen.Add(episodeID))
                        continue;

                    var seasonNumber = AsInt(row[3]);
                    var crossSourceIDs = AsInt(row[11]) is > 0 and var tvdbID ? new List<MetadataGuid> { CrossSource("tvdb", MetadataEntityType.Episode, Text(tvdbID)) } : [];
                    rows.Add([
                        TmdbNumber,
                        Text(episodeID),
                        Text(AsInt(row[1])),
                        AsInt(row[2]) is > 0 and var seasonID ? Text(seasonID) : null,
                        seasonNumber,
                        AsInt(row[4]),
                        (int)(seasonNumber is 0 ? EpisodeType.Special : EpisodeType.Episode),
                        Math.Round(AsDouble(row[6]), 2),
                        AsInt(row[7]),
                        row[5] is null ? 0 : AsInt(row[5]) * 60,
                        row[8],
                        row[10],
                        ToJson(crossSourceIDs),
                        AsBool(row[12]) ? 1 : 0,
                        row[9],
                    ]);
                }

                copied += TmdbInsert(transaction, "Metadata_Episode",
                    ["Source", "ProviderID", "SeriesID", "SeasonID", "SeasonNumber", "EpisodeNumber", "Type", "Rating", "RatingVotes", "Runtime", "AirDate", "LastUpdatedAt",
                        "CrossSourceIDs", "IsHidden", "CreatedAt"],
                    rows);
            }

            return copied;
        });

    /// <summary>
    ///   Copies TMDB's movies from <c>TMDB_Movie</c> into <c>Metadata_Movie</c>,
    ///   with their IMDb IDs as cross-source IDs, their runtimes, production
    ///   countries, collections, stored or not, and the time each was last
    ///   refreshed.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbMovies(object connection)
        => RunTmdbDataCopy(connection, "movies", "Metadata_Movie", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Movie WHERE Source = {TmdbNumber}");
            var seen = new HashSet<int>();
            var rows = new List<object?[]>();
            var columns = "TmdbMovieID, OriginalLanguageCode, IsRestricted, IsVideo, Runtime, UserRating, UserVotes, ReleasedAt, CreatedAt, LastUpdatedAt, " +
                "ImdbMovieID, ProductionCountries, TmdbCollectionID";
            foreach (var row in Read(transaction, $"SELECT {columns} FROM TMDB_Movie ORDER BY TMDB_MovieID"))
            {
                var movieID = AsInt(row[0]);
                if (movieID <= 0 || !seen.Add(movieID))
                    continue;

                var crossSourceIDs = ImdbID(row[10]) is { } imdbID ?new List<MetadataGuid> { CrossSource("imdb", MetadataEntityType.Movie, imdbID) } : [];
                rows.Add([
                    TmdbNumber,
                    Text(movieID),
                    row[7],
                    AsBool(row[2]) ? 1 : 0,
                    AsBool(row[3]) ? 1 : 0,
                    Math.Round(AsDouble(row[5]), 2),
                    AsInt(row[6]),
                    ToJson(crossSourceIDs),
                    row[9],
                    NullIfEmpty(row[1]),
                    ToExtraJson(new Metadata_MovieExtra
                    {
                        ProductionCountries = CountryCodes(row[11]),
                        CollectionID = AsInt(row[12]) is > 0 and var collectionID ? Text(collectionID) : null,
                    }.NullIfEmpty()),
                    row[8],
                    row[4] is null ? null : AsInt(row[4]) * 60,
                    RefreshedAt(row[8], row[9]),
                ]);
            }

            return TmdbInsert(transaction, "Metadata_Movie",
                ["Source", "ProviderID", "ReleasedAt", "IsRestricted", "IsVideo", "Rating", "RatingVotes", "CrossSourceIDs", "LastUpdatedAt", "OriginalLanguageCode", "ExtraData",
                    "CreatedAt", "Runtime", "LastRefreshedAt"],
                rows);
        });

    /// <summary>
    ///   Copies TMDB's movie collections from <c>TMDB_Collection</c> into
    ///   <c>Metadata_Collection</c>.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbCollections(object connection)
        => RunTmdbDataCopy(connection, "collections", "Metadata_Collection", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Collection WHERE Source = {TmdbNumber}");
            var seen = new HashSet<int>();
            var rows = new List<object?[]>();
            foreach (var row in Read(transaction, "SELECT TmdbCollectionID, CreatedAt, LastUpdatedAt FROM TMDB_Collection ORDER BY TMDB_CollectionID"))
            {
                var collectionID = AsInt(row[0]);
                if (collectionID > 0 && seen.Add(collectionID))
                    rows.Add([TmdbNumber, Text(collectionID), row[2], row[1], RefreshedAt(row[1], row[2])]);
            }

            return TmdbInsert(transaction, "Metadata_Collection", ["Source", "ProviderID", "LastUpdatedAt", "CreatedAt", "LastRefreshedAt"], rows);
        });

    /// <summary>
    ///   Copies the movies of TMDB's collections from
    ///   <c>TMDB_Collection_Movie</c> into <c>Metadata_Collection_Member</c>,
    ///   in their order, followed by any movie naming a stored collection that
    ///   lists it nowhere.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbCollectionMovies(object connection)
        => RunTmdbDataCopy(connection, "collection movies", "Metadata_Collection_Member", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Collection_Member WHERE Source = {TmdbNumber}");
            var collections = ReadIDs(transaction, "SELECT TmdbCollectionID FROM TMDB_Collection").ToHashSet();
            var movies = new Dictionary<int, List<int>>();
            foreach (var row in Read(transaction, "SELECT TmdbCollectionID, TmdbMovieID FROM TMDB_Collection_Movie ORDER BY TmdbCollectionID, Ordering, TMDB_Collection_MovieID"))
                AddMember(movies, collections, AsInt(row[0]), AsInt(row[1]));
            foreach (var row in Read(transaction, "SELECT TmdbCollectionID, TmdbMovieID FROM TMDB_Movie WHERE TmdbCollectionID IS NOT NULL ORDER BY TmdbMovieID"))
                AddMember(movies, collections, AsInt(row[0]), AsInt(row[1]));

            var movieType = KindNumber(MetadataEntityType.Movie);
            var rows = movies
                .OrderBy(pair => pair.Key)
                .SelectMany(pair => pair.Value.Select((movieID, ordering) => new object?[] { TmdbNumber, Text(pair.Key), movieType, Text(movieID), ordering }))
                .ToList();
            return TmdbInsert(transaction, "Metadata_Collection_Member", ["Source", "CollectionID", "MemberType", "MemberID", "Ordering"], rows);

            static void AddMember(Dictionary<int, List<int>> movies, HashSet<int> collections, int collectionID, int movieID)
            {
                if (movieID <= 0 || !collections.Contains(collectionID))
                    return;

                if (!movies.TryGetValue(collectionID, out var list))
                    movies[collectionID] = list = [];
                if (!list.Contains(movieID))
                    list.Add(movieID);
            }
        });

    /// <summary>
    ///   Copies the content ratings of TMDB's shows and movies into
    ///   <c>Metadata_ContentRating</c>, every one in its order.
    /// </summary>
    /// <remarks>
    ///   A country may keep several ratings; only an exact repeat of a
    ///   country and rating is left out, as the store leaves it out.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbContentRatings(object connection)
        => RunTmdbDataCopy(connection, "content ratings", "Metadata_ContentRating", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_ContentRating WHERE Source = {TmdbNumber}");
            var rows = new List<object?[]>();
            foreach (var (table, idColumn, entityType) in (ReadOnlySpan<(string, string, MetadataEntityType)>)[("TMDB_Show", "TmdbShowID", MetadataEntityType.Series), ("TMDB_Movie", "TmdbMovieID", MetadataEntityType.Movie)])
            {
                var seen = new HashSet<int>();
                foreach (var row in Read(transaction, $"SELECT {idColumn}, ContentRatings FROM {table} ORDER BY {table}ID"))
                {
                    var id = AsInt(row[0]);
                    if (id <= 0 || !seen.Add(id))
                        continue;

                    var kept = new HashSet<(string Country, string Rating)>();
                    var ordering = 0;
                    foreach (var rating in (row[1] as string ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var parts = rating.Split(',', 2, StringSplitOptions.TrimEntries);
                        if (parts.Length < 2 || parts[0].Length is 0 || parts[1].Length is 0 || !kept.Add((parts[0].ToUpperInvariant(), parts[1])))
                            continue;

                        rows.Add([TmdbNumber, KindNumber(entityType), Text(id), parts[0], parts[0].FromIso3166ToIso639() ?? string.Empty, parts[1], ordering++]);
                    }
                }
            }

            return TmdbInsert(transaction, "Metadata_ContentRating", ["Source", "EntityType", "EntityID", "CountryCode", "LanguageCode", "Rating", "Ordering"], rows);
        });

    /// <summary>
    ///   Copies the genres and keywords of TMDB's shows and movies into
    ///   <c>Metadata_Tag</c> as provisional tags keyed by their names, and
    ///   links each entry to its own, genres first, in their order.
    /// </summary>
    /// <remarks>
    ///   TMDB's tables kept the names only, with a genre such as
    ///   <c>Sci-Fi &amp; Fantasy</c> already split in two. The source's next
    ///   refresh of an entry links it to tags keyed by TMDB's numbers, and the
    ///   purge of unused entries removes the provisional ones.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbTags(object connection)
        => RunTmdbDataCopy(connection, "genres and keywords", "Metadata_Tag", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Tag_Entry WHERE Source = {TmdbNumber}");
            Execute(transaction, $"DELETE FROM Metadata_Tag WHERE Source = {TmdbNumber}");
            var tags = new Dictionary<string, (string Name, TagKind Kind)>(StringComparer.Ordinal);
            var links = new List<(int EntityType, int ID, List<string> Tags)>();
            foreach (var (table, idColumn, entityType) in (ReadOnlySpan<(string, string, MetadataEntityType)>)[("TMDB_Show", "TmdbShowID", MetadataEntityType.Series), ("TMDB_Movie", "TmdbMovieID", MetadataEntityType.Movie)])
            {
                var seen = new HashSet<int>();
                foreach (var row in Read(transaction, $"SELECT {idColumn}, Genres, Keywords FROM {table} ORDER BY {table}ID"))
                {
                    var id = AsInt(row[0]);
                    if (id <= 0 || !seen.Add(id))
                        continue;

                    var entryTags = new List<string>();
                    foreach (var (column, kind) in (ReadOnlySpan<(int, TagKind)>)[(1, TagKind.Genre), (2, TagKind.Keyword)])
                    {
                        foreach (var name in (row[column] as string ?? string.Empty).Split("|||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            var tagID = ProvisionalTmdbTagID(name, kind);
                            tags.TryAdd(tagID, (name, kind));
                            if (!entryTags.Contains(tagID))
                                entryTags.Add(tagID);
                        }
                    }

                    if (entryTags.Count > 0)
                        links.Add((KindNumber(entityType), id, entryTags));
                }
            }

            var now = DateTime.Now;
            var copied = TmdbInsert(transaction, "Metadata_Tag",
                ["Source", "ProviderID", "Name", "Description", "Kind", "IsSpoiler", "IsRestricted", "LastUpdatedAt"],
                tags.Select(pair => new object?[] { TmdbNumber, pair.Key, pair.Value.Name, string.Empty, (int)pair.Value.Kind, 0, 0, now }));
            var rowIDs = ReadTmdbRowIDs(transaction, "Metadata_Tag", "Metadata_TagID");
            TmdbInsert(transaction, "Metadata_Tag_Entry",
                ["Source", "EntityType", "EntityID", "TagID", "IsSpoiler", "Ordering"],
                links.SelectMany(link => link.Tags.Select((tagID, ordering) => new object?[] { TmdbNumber, link.EntityType, Text(link.ID), rowIDs[tagID], 0, ordering })));
            return copied;
        });

    #endregion

    #region TMDB Data | People, Studios and Networks

    /// <summary>
    ///   Copies TMDB's people from <c>TMDB_Person</c> into
    ///   <c>Metadata_Creator</c>, with their creation date, place of birth,
    ///   adult flag and IMDb link, and adds a stub, created when copied, for
    ///   each person a credit names but TMDB's table does not hold.
    /// </summary>
    /// <remarks>
    ///   A table created before it had its <c>CreatedAt</c> column gets the
    ///   column first; the later step that adds it then finds it there.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbPeople(object connection)
    {
        var added = connection switch
        {
            SqliteConnection => SQLite.AddCreatedAtIfMissing(connection, "Metadata_Creator"),
            MySqlConnection => MySQL.AddCreatedAtIfMissing(connection, "Metadata_Creator"),
            SqlConnection => SQLServer.AddCreatedAtIfMissing(connection, "Metadata_Creator"),
            _ => new(true, null),
        };
        return added.Item1 ? CopyTmdbPeopleRows(connection) : added;
    }

    /// <summary>
    ///   Copies TMDB's people into <c>Metadata_Creator</c>, as
    ///   <see cref="CopyTmdbPeople"/> tells.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    private static Tuple<bool, string?> CopyTmdbPeopleRows(object connection)
        => RunTmdbDataCopy(connection, "people", "Metadata_Creator", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Creator WHERE Source = {TmdbNumber}");
            var now = DateTime.Now;
            var seen = new HashSet<int>();
            var rows = new List<object?[]>();
            var columns = "TmdbPersonID, EnglishName, EnglishBiography, Gender, IsRestricted, BirthDay, DeathDay, PlaceOfBirth, CreatedAt, LastUpdatedAt, LastOrphanedAt, ImdbPersonID";
            foreach (var row in Read(transaction, $"SELECT {columns} FROM TMDB_Person ORDER BY TMDB_PersonID"))
            {
                var personID = AsInt(row[0]);
                if (personID <= 0 || !seen.Add(personID))
                    continue;

                var resources = NullIfEmpty(row[11]) is { } imdbID
                    ? new List<Resource> { new() { Type = ResourceType.CrossReference, Name = "IMDb", Url = $"https://www.imdb.com/name/{imdbID}/", ID = imdbID } }
                    : [];
                rows.Add([
                    TmdbNumber,
                    Text(personID),
                    row[1] as string ?? string.Empty,
                    NullIfEmpty(row[2]),
                    (int)CreatorType.Person,
                    AsDate(row[5]) is { } birthDay ? new FuzzyDateOnly(birthDay).ToString() : null,
                    AsDate(row[6]) is { } deathDay ? new FuzzyDateOnly(deathDay).ToString() : null,
                    row[9],
                    AsInt(row[3]),
                    resources.Count > 0 ? new JsonListConverter<Resource>().ConvertTo(null, null, resources, typeof(string)) : null,
                    row[10],
                    AsBool(row[4]) ? 1 : 0,
                    ToExtraJson(new Metadata_CreatorExtra { PlaceOfBirth = NullIfEmpty(row[7]) }.NullIfEmpty()),
                    RefreshedAt(row[8], row[9]),
                    row[8],
                ]);
            }

            // People a credit names without TMDB's table holding them, as stubs.
            var credited = ReadIDs(transaction,
                "SELECT DISTINCT TmdbPersonID FROM TMDB_Episode_Cast UNION SELECT DISTINCT TmdbPersonID FROM TMDB_Episode_Crew " +
                "UNION SELECT DISTINCT TmdbPersonID FROM TMDB_Movie_Cast UNION SELECT DISTINCT TmdbPersonID FROM TMDB_Movie_Crew");
            foreach (var personID in credited.Where(id => id > 0 && seen.Add(id)).Order())
                rows.Add([TmdbNumber, Text(personID), string.Empty, null, (int)CreatorType.Person, null, null, null, 0, null, null, 0, null, null, now]);

            return TmdbInsert(transaction, "Metadata_Creator",
                ["Source", "ProviderID", "Name", "Description", "Type", "BirthDay", "DeathDay", "LastUpdatedAt", "Gender", "Resources", "LastOrphanedAt", "IsRestricted",
                    "ExtraData", "LastRefreshedAt", "CreatedAt"],
                rows);
        });

    /// <summary>
    ///   Copies TMDB's companies from <c>TMDB_Company</c> into
    ///   <c>Metadata_Studio</c>, with a stub for each company a link names but
    ///   the table does not hold.
    /// </summary>
    /// <remarks>
    ///   TMDB's table keeps no times, so a company counts as written and
    ///   refreshed when it is copied.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbCompanies(object connection)
        => RunTmdbDataCopy(connection, "companies", "Metadata_Studio", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Studio WHERE Source = {TmdbNumber}");
            var now = DateTime.Now;
            var seen = new HashSet<int>();
            var rows = new List<object?[]>();
            foreach (var row in Read(transaction, "SELECT TmdbCompanyID, Name, CountryOfOrigin FROM TMDB_Company ORDER BY TMDB_CompanyID"))
            {
                var companyID = AsInt(row[0]);
                if (companyID > 0 && seen.Add(companyID))
                    rows.Add([TmdbNumber, Text(companyID), row[1] as string ?? string.Empty, now, NullIfEmpty(row[2]), now]);
            }

            foreach (var companyID in ReadIDs(transaction, "SELECT DISTINCT TmdbCompanyID FROM TMDB_Company_Entity").Where(id => id > 0 && seen.Add(id)).Order())
                rows.Add([TmdbNumber, Text(companyID), string.Empty, null, null, null]);

            return TmdbInsert(transaction, "Metadata_Studio", ["Source", "ProviderID", "Name", "LastUpdatedAt", "CountryOfOrigin", "LastRefreshedAt"], rows);
        });

    /// <summary>
    ///   Copies the links between TMDB's companies and its shows and movies
    ///   from <c>TMDB_Company_Entity</c> into <c>Metadata_Studio_Entry</c>,
    ///   in their order.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbCompanyLinks(object connection)
        => RunTmdbDataCopy(connection, "company links", "Metadata_Studio_Entry", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Studio_Entry WHERE Source = {TmdbNumber}");
            var studios = ReadTmdbRowIDs(transaction, "Metadata_Studio", "Metadata_StudioID");
            var entries = new Dictionary<(int, int), List<int>>();
            foreach (var row in Read(transaction, "SELECT TmdbCompanyID, TmdbEntityType, TmdbEntityID FROM TMDB_Company_Entity ORDER BY TmdbEntityType, TmdbEntityID, Ordering, TMDB_Company_EntityID"))
            {
                var entityType = AsInt(row[1]) switch
                {
                    2 => KindNumber(MetadataEntityType.Movie),
                    4 => KindNumber(MetadataEntityType.Series),
                    _ => -1,
                };
                if (entityType < 0 || !studios.TryGetValue(Text(AsInt(row[0])), out var studioID))
                    continue;

                var key = (entityType, AsInt(row[2]));
                if (!entries.TryGetValue(key, out var list))
                    entries[key] = list = [];
                if (!list.Contains(studioID))
                    list.Add(studioID);
            }

            var rows = entries.SelectMany(pair => pair.Value.Select((studioID, ordering) =>
                new object?[] { TmdbNumber, pair.Key.Item1, Text(pair.Key.Item2), studioID, (int)StudioType.None, ordering }));
            return TmdbInsert(transaction, "Metadata_Studio_Entry", ["Source", "EntityType", "EntityID", "StudioID", "StudioType", "Ordering"], rows);
        });

    /// <summary>
    ///   Copies TMDB's networks from <c>TMDB_Network</c> into
    ///   <c>Metadata_Network</c>, with a stub for each network a show or an
    ///   alternate ordering names but the table does not hold.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbNetworks(object connection)
        => RunTmdbDataCopy(connection, "networks", "Metadata_Network", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Network WHERE Source = {TmdbNumber}");
            var now = DateTime.Now;
            var seen = new HashSet<int>();
            var rows = new List<object?[]>();
            foreach (var row in Read(transaction, "SELECT TmdbNetworkID, Name, CountryOfOrigin, LastOrphanedAt FROM TMDB_Network ORDER BY TMDB_NetworkID"))
            {
                var networkID = AsInt(row[0]);
                if (networkID > 0 && seen.Add(networkID))
                    rows.Add([TmdbNumber, Text(networkID), row[1] as string ?? string.Empty, now, row[3], NullIfEmpty(row[2]), now]);
            }

            var named = ReadIDs(transaction, "SELECT DISTINCT TmdbNetworkID FROM TMDB_Show_Network")
                .Concat(ReadIDs(transaction, "SELECT DISTINCT TmdbNetworkID FROM TMDB_AlternateOrdering WHERE TmdbNetworkID IS NOT NULL"));
            foreach (var networkID in named.Where(id => id > 0 && seen.Add(id)).Order())
                rows.Add([TmdbNumber, Text(networkID), string.Empty, null, null, null, null]);

            return TmdbInsert(transaction, "Metadata_Network", ["Source", "ProviderID", "Name", "LastUpdatedAt", "LastOrphanedAt", "CountryOfOrigin", "LastRefreshedAt"], rows);
        });

    /// <summary>
    ///   Copies the networks of TMDB's shows from <c>TMDB_Show_Network</c>
    ///   into <c>Metadata_Network_Entry</c>, in their order.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbShowNetworks(object connection)
        => RunTmdbDataCopy(connection, "show networks", "Metadata_Network_Entry", (transaction, report) =>
        {
            var series = KindNumber(MetadataEntityType.Series);
            Execute(transaction, $"DELETE FROM Metadata_Network_Entry WHERE Source = {TmdbNumber} AND EntityType = {series}");
            var networks = ReadTmdbRowIDs(transaction, "Metadata_Network", "Metadata_NetworkID");
            var shows = new Dictionary<int, List<int>>();
            foreach (var row in Read(transaction, "SELECT TmdbShowID, TmdbNetworkID FROM TMDB_Show_Network ORDER BY TmdbShowID, Ordering, TMDB_Show_NetworkID"))
            {
                if (!networks.TryGetValue(Text(AsInt(row[1])), out var networkID))
                    continue;

                var showID = AsInt(row[0]);
                if (!shows.TryGetValue(showID, out var list))
                    shows[showID] = list = [];
                if (!list.Contains(networkID))
                    list.Add(networkID);
            }

            var rows = shows.SelectMany(pair => pair.Value.Select((networkID, ordering) => new object?[] { TmdbNumber, series, Text(pair.Key), networkID, ordering }));
            return TmdbInsert(transaction, "Metadata_Network_Entry", ["Source", "EntityType", "EntityID", "NetworkID", "Ordering"], rows);
        });

    #endregion

    #region TMDB Data | Credits

    /// <summary>
    ///   Copies TMDB's episode cast from <c>TMDB_Episode_Cast</c> into
    ///   <c>Metadata_Cast</c>, in each episode's order, with the show's
    ///   original language as the language of each performance.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbEpisodeCast(object connection)
        => RunTmdbDataCopy(connection, "episode cast", "Metadata_Cast", (transaction, report) =>
        {
            var episode = KindNumber(MetadataEntityType.Episode);
            Execute(transaction, $"DELETE FROM Metadata_Cast WHERE Source = {TmdbNumber} AND EntityType = {episode}");
            var creators = ReadTmdbRowIDs(transaction, "Metadata_Creator", "Metadata_CreatorID");
            var languages = ReadTmdbLanguages(transaction, "TMDB_Show", "TmdbShowID");
            var copied = 0;
            var columns = "TmdbEpisodeID, TmdbShowID, TmdbPersonID, CharacterName, IsGuestRole";
            foreach (var page in ReadTmdbPages(transaction, "TMDB_Episode_Cast", "TmdbEpisodeID", columns, "TmdbEpisodeID, IsGuestRole, Ordering, TMDB_Episode_CastID", report, "Copying TMDB episode cast"))
            {
                var rows = page.GroupBy(row => AsInt(row[0])).SelectMany(group => group.Select((row, ordering) =>
                    CastRow(episode, group.Key, creators, AsInt(row[2]), row[3], AsBool(row[4]), languages.GetValueOrDefault(AsInt(row[1])), ordering)));
                copied += TmdbInsert(transaction, "Metadata_Cast", _castColumns, rows);
            }

            return copied;
        });

    /// <summary>
    ///   Copies TMDB's episode crew from <c>TMDB_Episode_Crew</c> into
    ///   <c>Metadata_Crew</c>, by department and job, each named as
    ///   <c>Department, Job</c>.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbEpisodeCrew(object connection)
        => RunTmdbDataCopy(connection, "episode crew", "Metadata_Crew", (transaction, report) =>
        {
            var episode = KindNumber(MetadataEntityType.Episode);
            Execute(transaction, $"DELETE FROM Metadata_Crew WHERE Source = {TmdbNumber} AND EntityType = {episode}");
            var creators = ReadTmdbRowIDs(transaction, "Metadata_Creator", "Metadata_CreatorID");
            var languages = ReadTmdbLanguages(transaction, "TMDB_Show", "TmdbShowID");
            var copied = 0;
            var columns = "TmdbEpisodeID, TmdbShowID, TmdbPersonID, Department, Job";
            foreach (var page in ReadTmdbPages(transaction, "TMDB_Episode_Crew", "TmdbEpisodeID", columns, "TmdbEpisodeID, Department, Job, TmdbCreditID, TMDB_Episode_CrewID", report, "Copying TMDB episode crew"))
            {
                var rows = page.GroupBy(row => AsInt(row[0])).SelectMany(group => group
                    .DistinctBy(row => (AsInt(row[2]), $"{row[3]}, {row[4]}"))
                    .Select((row, ordering) => CrewRow(episode, group.Key, creators, AsInt(row[2]), row[3], row[4], languages.GetValueOrDefault(AsInt(row[1])), ordering))).WhereNotNull();
                copied += TmdbInsert(transaction, "Metadata_Crew", _crewColumns, rows);
            }

            return copied;
        });

    /// <summary>
    ///   Copies TMDB's movie cast from <c>TMDB_Movie_Cast</c> into
    ///   <c>Metadata_Cast</c>, in each movie's order.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbMovieCast(object connection)
        => RunTmdbDataCopy(connection, "movie cast", "Metadata_Cast", (transaction, report) =>
        {
            var movie = KindNumber(MetadataEntityType.Movie);
            Execute(transaction, $"DELETE FROM Metadata_Cast WHERE Source = {TmdbNumber} AND EntityType = {movie}");
            var creators = ReadTmdbRowIDs(transaction, "Metadata_Creator", "Metadata_CreatorID");
            var languages = ReadTmdbLanguages(transaction, "TMDB_Movie", "TmdbMovieID");
            var copied = 0;
            foreach (var page in ReadTmdbPages(transaction, "TMDB_Movie_Cast", "TmdbMovieID", "TmdbMovieID, TmdbPersonID, CharacterName", "TmdbMovieID, Ordering, TMDB_Movie_CastID", report, "Copying TMDB movie cast"))
            {
                var rows = page.GroupBy(row => AsInt(row[0])).SelectMany(group => group.Select((row, ordering) =>
                    CastRow(movie, group.Key, creators, AsInt(row[1]), row[2], false, languages.GetValueOrDefault(group.Key), ordering)));
                copied += TmdbInsert(transaction, "Metadata_Cast", _castColumns, rows);
            }

            return copied;
        });

    /// <summary>
    ///   Copies TMDB's movie crew from <c>TMDB_Movie_Crew</c> into
    ///   <c>Metadata_Crew</c>, by department and job.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbMovieCrew(object connection)
        => RunTmdbDataCopy(connection, "movie crew", "Metadata_Crew", (transaction, report) =>
        {
            var movie = KindNumber(MetadataEntityType.Movie);
            Execute(transaction, $"DELETE FROM Metadata_Crew WHERE Source = {TmdbNumber} AND EntityType = {movie}");
            var creators = ReadTmdbRowIDs(transaction, "Metadata_Creator", "Metadata_CreatorID");
            var languages = ReadTmdbLanguages(transaction, "TMDB_Movie", "TmdbMovieID");
            var copied = 0;
            foreach (var page in ReadTmdbPages(transaction, "TMDB_Movie_Crew", "TmdbMovieID", "TmdbMovieID, TmdbPersonID, Department, Job", "TmdbMovieID, Department, Job, TmdbCreditID, TMDB_Movie_CrewID", report, "Copying TMDB movie crew"))
            {
                var rows = page.GroupBy(row => AsInt(row[0])).SelectMany(group => group
                    .DistinctBy(row => (AsInt(row[1]), $"{row[2]}, {row[3]}"))
                    .Select((row, ordering) => CrewRow(movie, group.Key, creators, AsInt(row[1]), row[2], row[3], languages.GetValueOrDefault(group.Key), ordering))).WhereNotNull();
                copied += TmdbInsert(transaction, "Metadata_Crew", _crewColumns, rows);
            }

            return copied;
        });

    /// <summary>
    ///   Works out the cast of each TMDB show and season from its episodes'
    ///   cast and stores it in <c>Metadata_Cast</c>: one credit per person,
    ///   character and kind of role, in the order TMDB's own models gave them.
    /// </summary>
    /// <remarks>
    ///   The source's provider stores these itself from now on, so they are
    ///   ready when read rather than gathered from every episode each time.
    ///   Each credit takes its place on its first episode by TMDB's ID; a
    ///   show's are by that place, then person, and a season's by person,
    ///   then that place.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> AggregateTmdbShowCast(object connection)
        => RunTmdbDataCopy(connection, "show and season cast", "Metadata_Cast", (transaction, report) =>
        {
            var series = KindNumber(MetadataEntityType.Series);
            var season = KindNumber(MetadataEntityType.Season);
            Execute(transaction, $"DELETE FROM Metadata_Cast WHERE Source = {TmdbNumber} AND EntityType IN ({series}, {season})");
            var creators = ReadTmdbRowIDs(transaction, "Metadata_Creator", "Metadata_CreatorID");
            var languages = ReadTmdbLanguages(transaction, "TMDB_Show", "TmdbShowID");
            var copied = 0;
            var columns = "TmdbShowID, TmdbSeasonID, TmdbEpisodeID, TmdbPersonID, CharacterName, IsGuestRole, Ordering";
            foreach (var page in ReadTmdbPages(transaction, "TMDB_Episode_Cast", "TmdbShowID", columns, "TmdbShowID, TmdbEpisodeID, Ordering, TMDB_Episode_CastID", report, "Gathering TMDB show cast", 200))
            {
                var rows = new List<object?[]>();
                foreach (var show in page.GroupBy(row => AsInt(row[0])))
                {
                    var language = languages.GetValueOrDefault(show.Key);
                    rows.AddRange(AggregateCast(show)
                        .OrderBy(credit => credit.Place)
                        .ThenBy(credit => credit.PersonID)
                        .Select((credit, ordering) => CastRow(series, show.Key, creators, credit.PersonID, credit.Character, credit.Guest, language, ordering)));

                    foreach (var seasonRows in show.GroupBy(row => AsInt(row[1])).OrderBy(group => group.Key))
                        rows.AddRange(AggregateCast(seasonRows)
                            .OrderBy(credit => credit.PersonID)
                            .ThenBy(credit => credit.Place)
                            .Select((credit, ordering) => CastRow(season, seasonRows.Key, creators, credit.PersonID, credit.Character, credit.Guest, language, ordering)));
                }

                copied += TmdbInsert(transaction, "Metadata_Cast", _castColumns, rows);
            }

            return copied;

            // Each person in each role once, as first seen, with their place on that episode.
            static IEnumerable<(int PersonID, string Character, bool Guest, int Place)> AggregateCast(IEnumerable<object?[]> rows)
                => rows
                    .GroupBy(row => (Person: AsInt(row[3]), Character: row[4] as string ?? string.Empty, Guest: AsBool(row[5])))
                    .Select(group => (group.Key.Person, group.Key.Character, group.Key.Guest, Place: AsInt(group.First()[6])));
        });

    /// <summary>
    ///   Works out the crew of each TMDB show and season from its episodes'
    ///   crew and stores it in <c>Metadata_Crew</c>: one credit per person,
    ///   department and job, in the order TMDB's own models gave them.
    /// </summary>
    /// <remarks>
    ///   A show's are by department, job and person, and a season's by
    ///   person, job and department.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> AggregateTmdbShowCrew(object connection)
        => RunTmdbDataCopy(connection, "show and season crew", "Metadata_Crew", (transaction, report) =>
        {
            var series = KindNumber(MetadataEntityType.Series);
            var season = KindNumber(MetadataEntityType.Season);
            Execute(transaction, $"DELETE FROM Metadata_Crew WHERE Source = {TmdbNumber} AND EntityType IN ({series}, {season})");
            var creators = ReadTmdbRowIDs(transaction, "Metadata_Creator", "Metadata_CreatorID");
            var languages = ReadTmdbLanguages(transaction, "TMDB_Show", "TmdbShowID");
            var copied = 0;
            var columns = "TmdbShowID, TmdbSeasonID, TmdbPersonID, Department, Job";
            foreach (var page in ReadTmdbPages(transaction, "TMDB_Episode_Crew", "TmdbShowID", columns, "TmdbShowID, TmdbEpisodeID, Department, Job, TmdbCreditID, TMDB_Episode_CrewID", report, "Gathering TMDB show crew", 200))
            {
                var rows = new List<object?[]>();
                foreach (var show in page.GroupBy(row => AsInt(row[0])))
                {
                    var language = languages.GetValueOrDefault(show.Key);
                    rows.AddRange(AggregateCrew(show)
                        .OrderBy(credit => credit.Department, StringComparer.InvariantCulture)
                        .ThenBy(credit => credit.Job, StringComparer.InvariantCulture)
                        .ThenBy(credit => credit.PersonID)
                        .Select((credit, ordering) => CrewRow(series, show.Key, creators, credit.PersonID, credit.Department, credit.Job, language, ordering)).WhereNotNull());
                    foreach (var seasonRows in show.GroupBy(row => AsInt(row[1])).OrderBy(group => group.Key))
                        rows.AddRange(AggregateCrew(seasonRows)
                            .OrderBy(credit => credit.PersonID)
                            .ThenBy(credit => credit.Job, StringComparer.InvariantCulture)
                            .ThenBy(credit => credit.Department, StringComparer.InvariantCulture)
                            .Select((credit, ordering) => CrewRow(season, seasonRows.Key, creators, credit.PersonID, credit.Department, credit.Job, language, ordering)).WhereNotNull());
                }

                copied += TmdbInsert(transaction, "Metadata_Crew", _crewColumns, rows);
            }

            return copied;

            // Each person in each job once.
            static IEnumerable<(int PersonID, string Department, string Job)> AggregateCrew(IEnumerable<object?[]> rows)
                => rows
                    .Select(row => (Person: AsInt(row[2]), Department: row[3] as string ?? string.Empty, Job: row[4] as string ?? string.Empty))
                    .DistinctBy(credit => (credit.Person, $"{credit.Department}, {credit.Job}"));
        });

    /// <summary>
    ///   The columns every cast row is written with, in the order
    ///   <see cref="CastRow"/> gives them.
    /// </summary>
    private static readonly string[] _castColumns = ["Source", "EntityType", "EntityID", "CreatorID", "Name", "RoleType", "LanguageCode", "Ordering", "RoleNotes"];

    /// <summary>
    ///   The columns every crew row is written with, in the order
    ///   <see cref="CrewRow"/> gives them.
    /// </summary>
    private static readonly string[] _crewColumns = ["Source", "EntityType", "EntityID", "CreatorID", "Name", "RoleType", "LanguageCode", "Ordering"];

    /// <summary>
    ///   One cast row.
    /// </summary>
    /// <param name="entityType">The number of the entry's kind.</param>
    /// <param name="entityID">The entry's TMDB ID.</param>
    /// <param name="creators">The creators' row IDs, by TMDB ID.</param>
    /// <param name="personID">The person's TMDB ID.</param>
    /// <param name="character">The character's name.</param>
    /// <param name="guest">Whether it is a guest star's role.</param>
    /// <param name="languageCode">The language of the performance, or <c>null</c>.</param>
    /// <param name="ordering">Where the credit sits among the entry's.</param>
    /// <returns>The row's values.</returns>
    private static object?[] CastRow(int entityType, int entityID, IReadOnlyDictionary<string, int> creators, int personID, object? character, bool guest, string? languageCode, int ordering)
        => [
            TmdbNumber, entityType, Text(entityID), creators.TryGetValue(Text(personID), out var creatorID) ? creatorID : null, character as string ?? string.Empty,
            (int)CastRoleType.None, languageCode, ordering, guest ? TmdbGuestStarNotes : null,
        ];

    /// <summary>
    ///   The kind of a crew job, where TMDB's wording maps onto one.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>The kind, or <see cref="CrewRoleType.None"/>.</returns>
    private static CrewRoleType TmdbCrewRole(string? job)
        => job?.Trim().ToLowerInvariant() switch
        {
            "director" or "series director" => CrewRoleType.Director,
            "producer" or "executive producer" or "animation producer" => CrewRoleType.Producer,
            "series composition" => CrewRoleType.SeriesComposer,
            "character designer" or "original character design" => CrewRoleType.CharacterDesign,
            "original music composer" or "music" or "composer" => CrewRoleType.Music,
            "novel" or "original story" or "comic book" or "author" or "original concept" or "original series creator" => CrewRoleType.SourceWork,
            _ => CrewRoleType.None,
        };

    /// <summary>
    ///   One crew row, named <c>Department, Job</c>.
    /// </summary>
    /// <param name="entityType">The number of the entry's kind.</param>
    /// <param name="entityID">The entry's TMDB ID.</param>
    /// <param name="creators">The creators' row IDs, by TMDB ID.</param>
    /// <param name="personID">The person's TMDB ID.</param>
    /// <param name="department">The department.</param>
    /// <param name="job">The job.</param>
    /// <param name="languageCode">The language of the work, or <c>null</c>.</param>
    /// <param name="ordering">Where the credit sits among the entry's.</param>
    /// <returns>The row's values, or <c>null</c> when the person is not stored.</returns>
    private static object?[]? CrewRow(int entityType, int entityID, IReadOnlyDictionary<string, int> creators, int personID, object? department, object? job, string? languageCode, int ordering)
        => creators.TryGetValue(Text(personID), out var creatorID)
            ? [TmdbNumber, entityType, Text(entityID), creatorID, $"{department}, {job}", (int)TmdbCrewRole(job as string), languageCode, ordering]
            : null;

    #endregion

    #region TMDB Data | Orderings and Suggestions

    /// <summary>
    ///   Copies TMDB's alternate orderings from <c>TMDB_AlternateOrdering</c>
    ///   into <c>Metadata_Ordering</c>, leaving out those of no stored show.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbOrderings(object connection)
        => RunTmdbDataCopy(connection, "alternate orderings", "Metadata_Ordering", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Ordering WHERE Source = {TmdbNumber}");
            var shows = ReadIDs(transaction, "SELECT TmdbShowID FROM TMDB_Show").ToHashSet();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<object?[]>();
            foreach (var row in Read(transaction, "SELECT TmdbEpisodeGroupCollectionID, TmdbShowID, Type, EnglishTitle, EnglishOverview, CreatedAt, LastUpdatedAt FROM TMDB_AlternateOrdering ORDER BY TMDB_AlternateOrderingID"))
            {
                if (NullIfEmpty(row[0]) is not { } orderingID || !shows.Contains(AsInt(row[1])) || !seen.Add(orderingID))
                    continue;

                rows.Add([TmdbNumber, orderingID, TmdbNumber, Text(AsInt(row[1])), AsInt(row[2]), row[3] as string ?? string.Empty, NullIfEmpty(row[4]), row[5], row[6]]);
            }

            return TmdbInsert(transaction, "Metadata_Ordering", ["Source", "ProviderID", "SeriesSource", "SeriesID", "Type", "Name", "Description", "CreatedAt", "LastUpdatedAt"], rows);
        });

    /// <summary>
    ///   Copies the groups of TMDB's alternate orderings from
    ///   <c>TMDB_AlternateOrdering_Season</c> into
    ///   <c>Metadata_Ordering_Group</c>, in their order, with the first group
    ///   numbered 0 as the ordering's specials and the others keeping TMDB's
    ///   number above 0 as their own. Whether a group was locked is not kept.
    /// </summary>
    /// <remarks>
    ///   A table made before it had the season number column gets it first,
    ///   outside the copy's transaction.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbOrderingGroups(object connection)
    {
        var added = AddOrderingGroupSeasonNumber(connection);
        return !added.Item1 ? added : RunTmdbDataCopy(connection, "alternate ordering groups", "Metadata_Ordering_Group", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Ordering_Group WHERE Source = {TmdbNumber}");
            var orderings = ReadTmdbProviderIDs(transaction, "Metadata_Ordering");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<object?[]>();
            var groups = Read(transaction, "SELECT TmdbEpisodeGroupCollectionID, TmdbEpisodeGroupID, SeasonNumber, EnglishTitle FROM TMDB_AlternateOrdering_Season ORDER BY TmdbEpisodeGroupCollectionID, SeasonNumber, TMDB_AlternateOrdering_SeasonID")
                .Where(row => row[0] is string orderingID && orderings.Contains(orderingID))
                .GroupBy(row => (string)row[0]!, StringComparer.Ordinal);
            foreach (var ordering in groups)
            {
                var position = 0;
                var hasSpecials = false;
                foreach (var row in ordering)
                {
                    if (NullIfEmpty(row[1]) is not { } groupID || !seen.Add(groupID))
                        continue;

                    var number = AsInt(row[2]);
                    var isSpecial = number is 0 && !hasSpecials;
                    hasSpecials |= isSpecial;
                    rows.Add([TmdbNumber, groupID, ordering.Key, position++, row[3] as string ?? string.Empty, isSpecial ? 1 : 0, number > 0 ? number : null]);
                }
            }

            return TmdbInsert(transaction, "Metadata_Ordering_Group", ["Source", "ProviderID", "OrderingID", "Position", "Name", "IsSpecial", "SeasonNumber"],
                rows);
        });
    }

    /// <summary>
    ///   Adds the season number column to <c>Metadata_Ordering_Group</c> when
    ///   the table does not have it yet, as a table made before the column
    ///   was added does not.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> AddOrderingGroupSeasonNumber(object connection)
    {
        try
        {
            var dbConnection = (DbConnection)connection;
            if (dbConnection.State is not System.Data.ConnectionState.Open)
                dbConnection.Open();

            var (exists, add) = dbConnection switch
            {
                SqliteConnection => (
                    "SELECT COUNT(*) FROM pragma_table_info('Metadata_Ordering_Group') WHERE name = 'SeasonNumber';",
                    "ALTER TABLE Metadata_Ordering_Group ADD COLUMN SeasonNumber INTEGER NULL;"
                ),
                MySqlConnection => (
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Metadata_Ordering_Group' AND COLUMN_NAME = 'SeasonNumber';",
                    "ALTER TABLE `Metadata_Ordering_Group` ADD COLUMN `SeasonNumber` INT NULL;"
                ),
                _ => (
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'Metadata_Ordering_Group') AND name = N'SeasonNumber';",
                    "ALTER TABLE Metadata_Ordering_Group ADD SeasonNumber INT NULL;"
                ),
            };

            using var check = dbConnection.CreateCommand();
            check.CommandText = exists;
            if (Convert.ToInt64(check.ExecuteScalar()) is 0)
            {
                using var alter = dbConnection.CreateCommand();
                alter.CommandText = add;
                alter.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }

        return new(true, null);
    }

    /// <summary>
    ///   Copies the episodes of TMDB's alternate orderings from
    ///   <c>TMDB_AlternateOrdering_Episode</c> into
    ///   <c>Metadata_Ordering_Entry</c>, in each group's order, leaving out
    ///   episodes that are not stored.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbOrderingEpisodes(object connection)
        => RunTmdbDataCopy(connection, "alternate ordering episodes", "Metadata_Ordering_Entry", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Ordering_Entry WHERE Source = {TmdbNumber}");
            var groups = Read(transaction, $"SELECT ProviderID, OrderingID FROM Metadata_Ordering_Group WHERE Source = {TmdbNumber}")
                .ToDictionary(row => (string)row[0]!, row => (string)row[1]!, StringComparer.Ordinal);
            var episodes = ReadTmdbProviderIDs(transaction, "Metadata_Episode");
            var rows = new List<object?[]>();
            var places = Read(transaction, "SELECT TmdbEpisodeGroupID, TmdbEpisodeID FROM TMDB_AlternateOrdering_Episode ORDER BY TmdbEpisodeGroupID, EpisodeNumber, TMDB_AlternateOrdering_EpisodeID")
                .GroupBy(row => row[0] as string ?? string.Empty, StringComparer.Ordinal);
            foreach (var group in places)
            {
                if (!groups.TryGetValue(group.Key, out var orderingID))
                    continue;

                var position = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in group)
                {
                    var episodeID = Text(AsInt(row[1]));
                    if (episodes.Contains(episodeID) && seen.Add(episodeID))
                        rows.Add([TmdbNumber, orderingID, group.Key, position++, TmdbNumber, episodeID]);
                }
            }

            return TmdbInsert(transaction, "Metadata_Ordering_Entry", ["Source", "OrderingID", "GroupID", "Position", "EpisodeSource", "EpisodeID"], rows);
        });

    /// <summary>
    ///   Copies the network of each TMDB alternate ordering into
    ///   <c>Metadata_Network_Entry</c> as the ordering's network.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbOrderingNetworks(object connection)
        => RunTmdbDataCopy(connection, "alternate ordering networks", "Metadata_Network_Entry", (transaction, report) =>
        {
            var ordering = KindNumber(MetadataEntityType.Ordering);
            Execute(transaction, $"DELETE FROM Metadata_Network_Entry WHERE Source = {TmdbNumber} AND EntityType = {ordering}");
            var orderings = ReadTmdbProviderIDs(transaction, "Metadata_Ordering");
            var networks = ReadTmdbRowIDs(transaction, "Metadata_Network", "Metadata_NetworkID");
            var rows = new List<object?[]>();
            foreach (var row in Read(transaction, "SELECT TmdbEpisodeGroupCollectionID, TmdbNetworkID FROM TMDB_AlternateOrdering WHERE TmdbNetworkID IS NOT NULL ORDER BY TMDB_AlternateOrderingID"))
            {
                if (row[0] is string orderingID && orderings.Remove(orderingID) && networks.TryGetValue(Text(AsInt(row[1])), out var networkID))
                    rows.Add([TmdbNumber, ordering, orderingID, networkID, 0]);
            }

            return TmdbInsert(transaction, "Metadata_Network_Entry", ["Source", "EntityType", "EntityID", "NetworkID", "Ordering"], rows);
        });

    /// <summary>
    ///   Copies what TMDB recommends and finds similar from
    ///   <c>TMDB_Suggestion</c> into <c>Metadata_Suggestion</c>: the
    ///   recommendations first, each kind in its order.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbSuggestions(object connection)
        => RunTmdbDataCopy(connection, "suggestions", "Metadata_Suggestion", (transaction, report) =>
        {
            Execute(transaction, $"DELETE FROM Metadata_Suggestion WHERE Source = {TmdbNumber}");
            var seen = new HashSet<(int, int, int, int)>();
            var rows = Read(transaction, "SELECT TmdbEntityType, TmdbEntityID, SuggestedTmdbEntityID, Kind FROM TMDB_Suggestion ORDER BY TmdbEntityType, TmdbEntityID, Kind, Ordering, TMDB_SuggestionID")
                .Select(row => (Type: AsInt(row[0]), ID: AsInt(row[1]), Suggested: AsInt(row[2]), Kind: AsInt(row[3])))
                .Where(row => row.Suggested > 0 && seen.Add(row))
                .GroupBy(row => (row.Type, row.ID))
                .SelectMany(group => group.Select((row, ordering) => new object?[] { TmdbNumber, row.Type, Text(row.ID), row.Type, Text(row.Suggested), row.Kind, ordering }));
            return TmdbInsert(transaction, "Metadata_Suggestion", ["Source", "BaseType", "BaseID", "SuggestedType", "SuggestedID", "Kind", "Ordering"], rows);
        });

    #endregion

    #region TMDB Data | Texts

    /// <summary>
    ///   The TMDB tables whose rows keep an English title and overview, as
    ///   the text copies name them, with whether the rows keep an original
    ///   title, a season or episode number, or neither.
    /// </summary>
    private static readonly IReadOnlyList<(string Table, string IDColumn, MetadataEntityType EntityType, string? Extra)> _tmdbDefaultTextTables =
    [
        ("TMDB_Show", "TmdbShowID", MetadataEntityType.Series, "OriginalTitle, OriginalLanguageCode"),
        ("TMDB_Season", "TmdbSeasonID", MetadataEntityType.Season, "SeasonNumber"),
        ("TMDB_Episode", "TmdbEpisodeID", MetadataEntityType.Episode, "EpisodeNumber"),
        ("TMDB_Movie", "TmdbMovieID", MetadataEntityType.Movie, "OriginalTitle, OriginalLanguageCode"),
        ("TMDB_Collection", "TmdbCollectionID", MetadataEntityType.Collection, null),
    ];

    /// <summary>
    ///   Stores the English title each TMDB show, season, episode, movie and
    ///   collection kept on its row in <c>Metadata_Title</c>, as the entry's
    ///   main title ahead of its other TMDB titles, and adds a show's or
    ///   movie's original title in its original language when TMDB did not
    ///   list it.
    /// </summary>
    /// <remarks>
    ///   An episode's generic title with its own number, such as
    ///   <c>Episode 5</c>, is not stored, nor a season's generic name, such
    ///   as <c>Season 2</c>, nor an English text TMDB did not list unless it
    ///   really is English, as <see cref="CopyTmdbDefaultTexts"/> tells. A
    ///   run again finds the title it stored and stores it once more in the
    ///   same place.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbDefaultTitles(object connection)
        => RunTmdbDataCopy(connection, "default titles", "Metadata_Title", (transaction, report) =>
            _tmdbDefaultTextTables.Sum(table => CopyTmdbDefaultTexts(transaction, table, "Metadata_Title", "EnglishTitle", report)));

    /// <summary>
    ///   Stores the English overview each TMDB show, season, episode, movie
    ///   and collection kept on its row in <c>Metadata_Overview</c>, ahead of
    ///   its other TMDB overviews, so it is the entry's default.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbDefaultOverviews(object connection)
    {
        var result = RunTmdbDataCopy(connection, "default overviews", "Metadata_Overview", (transaction, report) =>
            _tmdbDefaultTextTables.Sum(table => CopyTmdbDefaultTexts(transaction, table, "Metadata_Overview", "EnglishOverview", report)));
        ReleaseCopyMemory(connection);
        return result;
    }

    /// <summary>
    ///   Removes the English titles of TMDB seasons that are only the
    ///   season's generic name, such as <c>Season 2</c>, which an earlier
    ///   copy stored when TMDB listed them. The core makes that name up.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> RemoveTmdbGenericSeasonTitles(object connection)
    {
        try
        {
            var dbConnection = (DbConnection)connection;
            if (dbConnection.State is not System.Data.ConnectionState.Open)
                dbConnection.Open();

            using var transaction = dbConnection.BeginTransaction();
            var generic = Read(transaction,
                    "SELECT t.Metadata_TitleID, t.Value, s.SeasonNumber FROM Metadata_Title t " +
                    "INNER JOIN Metadata_Season s ON s.Source = t.EntitySource AND s.ProviderID = t.EntityID " +
                    $"WHERE t.EntitySource = {TmdbNumber} AND t.Source = {TmdbNumber} AND t.EntityType = {KindNumber(MetadataEntityType.Season)} " +
                    "AND LOWER(t.LanguageCode) = 'en'")
                .Where(row => GenericEpisodeTitles.IsGenericSeasonName(row[1] as string, AsInt(row[2])))
                .Select(row => AsInt(row[0]))
                .ToList();
            foreach (var chunk in generic.Chunk(TmdbIDsPerList))
                Execute(transaction, $"DELETE FROM Metadata_Title WHERE Metadata_TitleID IN ({string.Join(", ", chunk)})");
            transaction.Commit();
            _logger.Info("Removed {Count} generic TMDB season titles from Metadata_Title.", generic.Count);
            return new(true, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }

    /// <summary>
    ///   Stores the English text the entries of one table kept on their rows
    ///   ahead of their other TMDB texts, a page of entries at a time.
    /// </summary>
    /// <remarks>
    ///   A text TMDB listed as American English is stored, unless it is a
    ///   season's generic name. One it did not list is TMDB's fallback when
    ///   it has no English text, so it is stored only when it is in the Latin
    ///   script and is not the original title, a generic season name or
    ///   another text of the entry; an overview only
    ///   when the entry has no English one, and a title behind the English
    ///   ones. An English text TMDB listed before the stored one stays ahead
    ///   of it. A title is always the main one: the English one, else the
    ///   original or repeated one, else TMDB's fallback in no known language.
    /// </remarks>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The entries' table, with what else its rows keep.</param>
    /// <param name="textTable">The title or overview table.</param>
    /// <param name="englishColumn">The column holding the English text.</param>
    /// <param name="report">Called with each progress message.</param>
    /// <returns>How many texts it stored.</returns>
    private static int CopyTmdbDefaultTexts(
        DbTransaction transaction,
        (string Table, string IDColumn, MetadataEntityType EntityType, string? Extra) table,
        string textTable,
        string englishColumn,
        Action<string> report
    )
    {
        var isTitle = textTable is "Metadata_Title";
        var idColumn = isTitle ? "Metadata_TitleID" : "Metadata_OverviewID";
        var entityType = KindNumber(table.EntityType);
        var extra = isTitle && table.Extra is not null ? $", {table.Extra}" : string.Empty;
        var entries = new Dictionary<string, object?[]>(StringComparer.Ordinal);
        foreach (var row in Read(transaction, $"SELECT {table.IDColumn}, {englishColumn}, {englishColumn}Listed{extra} FROM {table.Table} ORDER BY {table.Table}ID"))
            entries.TryAdd(Text(AsInt(row[0])), row);

        // What TMDB listed for the entries, in order, with the text only where it could be the English default or the original title.
        var hasOriginal = isTitle && table.Extra?.StartsWith("OriginalTitle", StringComparison.Ordinal) is true;
        var value = hasOriginal ? "Value" : "CASE WHEN LanguageCode = 'en' THEN Value ELSE NULL END";
        var stored = Read(transaction,
                $"SELECT EntityID, {idColumn}, Ordering, LanguageCode, CountryCode, {value} FROM {textTable} " +
                $"WHERE EntitySource = {TmdbNumber} AND Source = {TmdbNumber} AND EntityType = {entityType} ORDER BY EntityID, Ordering, {idColumn}")
            .GroupBy(row => (string)row[0]!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        // Every text of the entries whose English text TMDB did not list, which may repeat one of them.
        var unlisted = entries.Where(pair => !AsBool(pair.Value[2]) && NullIfEmpty(pair.Value[1]) is not null).Select(pair => pair.Key).ToList();
        var others = new Dictionary<string, List<(int ID, string Value)>>(StringComparer.Ordinal);
        foreach (var chunk in hasOriginal ? Enumerable.Empty<string[]>() : unlisted.Chunk(TmdbIDsPerList))
        {
            var sql = $"SELECT EntityID, {idColumn}, Value FROM {textTable} WHERE EntitySource = {TmdbNumber} AND Source = {TmdbNumber} AND EntityType = {entityType} " +
                $"AND EntityID IN ({string.Join(", ", chunk.Select(id => $"'{id}'"))})";
            foreach (var row in Read(transaction, sql))
            {
                if (!others.TryGetValue((string)row[0]!, out var list))
                    others[(string)row[0]!] = list = [];
                list.Add((AsInt(row[1]), row[2] as string ?? string.Empty));
            }
        }

        var inserted = new List<object?[]>();
        var removed = new List<int>();
        var shifted = new List<int>();
        var promoted = new List<int>();
        var progress = new StartupProgress(report, $"Storing TMDB {table.EntityType.Value} default {(isTitle ? "titles" : "overviews")}", entries.Count);
        foreach (var (entityID, row) in entries)
        {
            progress.Advance(1);
            var texts = stored.GetValueOrDefault(entityID) ?? [];
            var english = NullIfEmpty(row[1]);
            if (english is not null && table.EntityType == MetadataEntityType.Episode && isTitle && GenericEpisodeTitles.IsGeneric(english, EpisodeType.Episode, AsInt(row[3])))
                english = null;

            // A season's generic name TMDB listed is made up, not stored; one it did not list is its fallback, told below.
            if (english is not null && table.EntityType == MetadataEntityType.Season && isTitle && AsBool(row[2]) &&
                GenericEpisodeTitles.IsGenericSeasonName(english, AsInt(row[3])))
                english = null;

            // A text this step stored before, which it stores again in the same place.
            var earlier = english is null ? null : texts.FirstOrDefault(text => IsEnglishText(text, english));
            if (earlier is not null)
            {
                removed.Add(AsInt(earlier[1]));
                texts.Remove(earlier);
            }

            // A text TMDB did not list may be its fallback to another language's: the text it repeats is then the main
            // title, else the fallback is, in no known language. One that is English stays the main title behind the
            // English texts TMDB listed.
            int? mainID = null;
            string? unknownMain = null;
            var behindEnglish = false;
            var original = hasOriginal ? NullIfEmpty(row[3]) : null;
            var originalLanguage = hasOriginal ? NullIfEmpty(row[4]) : null;
            if (english is not null && !AsBool(row[2]))
            {
                var fallback = english;
                var repeated = hasOriginal
                    ? texts.FirstOrDefault(text => string.Equals(text[5] as string, fallback, StringComparison.Ordinal)) is { } text ? AsInt(text[1]) : (int?)null
                    : others.GetValueOrDefault(entityID)?.FirstOrDefault(text => text.ID != AsInt(earlier?[1]) && string.Equals(text.Value, fallback, StringComparison.Ordinal)) is { ID: > 0 } other
                        ? other.ID
                        : null;
                var generic = isTitle && table.EntityType == MetadataEntityType.Season && GenericEpisodeTitles.IsGenericSeasonName(fallback, AsInt(row[3]));
                if (originalLanguage is not "en" && (repeated is not null || !IsLatinText(fallback) || fallback == original || generic))
                {
                    english = null;
                    mainID = repeated;
                    if (isTitle && repeated is null && fallback != original && !generic)
                        unknownMain = fallback;
                }
                else if (texts.Any(text => text[3] as string is "en"))
                {
                    if (isTitle)
                        behindEnglish = true;
                    else
                        english = null;
                }
            }

            var englishOrdering = -1;
            if (english is not null && behindEnglish)
            {
                englishOrdering = texts.Max(text => AsInt(text[2])) + 1;
                inserted.Add(TextRow(entityType, entityID, TitleLanguage.EnglishAmerican.GetString(), "en", "US", TitleType.Main, english, englishOrdering));
            }
            else if (english is not null)
            {
                var gap = 0;
                while (gap < texts.Count && AsInt(texts[gap][2]) == gap)
                    gap++;

                // An English text listed before it keeps its place ahead; else the texts before the first gap make room at the front.
                if (texts.Take(gap).Any(text => text[3] as string is "en"))
                {
                    englishOrdering = gap;
                }
                else
                {
                    englishOrdering = 0;
                    shifted.AddRange(texts.Take(gap).Select(text => AsInt(text[1])));
                    for (var index = 0; index < gap; index++)
                        texts[index][2] = index + 1;
                }

                inserted.Add(TextRow(entityType, entityID, TitleLanguage.EnglishAmerican.GetString(), "en", "US", TitleType.Main, english, englishOrdering));
            }

            // Without an English title, the original title is the main one.
            if (original is not null && originalLanguage is not null && original != english &&
                !texts.Any(text => string.Equals(text[5] as string, original, StringComparison.Ordinal)))
            {
                var ordering = Math.Max(texts.Count > 0 ? texts.Max(text => AsInt(text[2])) + 1 : 0, englishOrdering + 1);
                var type = english is null && mainID is null ? TitleType.Main : TitleType.Official;
                inserted.Add(TextRow(entityType, entityID, TmdbTextLanguage(originalLanguage, null).GetString(), originalLanguage, null, type, original, ordering));
            }
            else if (english is null && mainID is null && original is not null &&
                texts.FirstOrDefault(text => string.Equals(text[5] as string, original, StringComparison.Ordinal)) is { } originalText)
            {
                mainID = AsInt(originalText[1]);
            }
            else if (unknownMain is not null && original is null)
            {
                var ordering = texts.Count > 0 ? texts.Max(text => AsInt(text[2])) + 1 : 0;
                inserted.Add(TextRow(entityType, entityID, TitleLanguage.Unknown.GetString(), "unk", null, TitleType.Main, unknownMain, ordering));
            }

            if (isTitle && mainID is { } promotedID)
                promoted.Add(promotedID);
        }

        foreach (var chunk in removed.Chunk(TmdbIDsPerList))
            Execute(transaction, $"DELETE FROM {textTable} WHERE {idColumn} IN ({string.Join(", ", chunk)})");
        foreach (var chunk in shifted.Chunk(TmdbIDsPerList))
            Execute(transaction, $"UPDATE {textTable} SET Ordering = Ordering + 1 WHERE {idColumn} IN ({string.Join(", ", chunk)})");
        foreach (var chunk in promoted.Chunk(TmdbIDsPerList))
            Execute(transaction, $"UPDATE {textTable} SET TitleType = {(int)TitleType.Main} WHERE {idColumn} IN ({string.Join(", ", chunk)})");

        var columns = isTitle
            ? new[] { "EntitySource", "EntityType", "EntityID", "Source", "Language", "LanguageCode", "CountryCode", "TitleType", "Value", "IsEnabled", "Preference", "Ordering" }
            : ["EntitySource", "EntityType", "EntityID", "Source", "Language", "LanguageCode", "CountryCode", "Value", "IsEnabled", "Preference", "Ordering"];
        return TmdbInsert(transaction, textTable, columns, isTitle ? inserted : inserted.Select(text => text.Where((_, index) => index != 7).ToArray()));

        static bool IsEnglishText(object?[] text, string english)
            => text[3] as string == "en" && text[4] as string == "US" && string.Equals(text[5] as string, english, StringComparison.Ordinal);

        static object?[] TextRow(int entityType, string entityID, string language, string languageCode, string? countryCode, TitleType titleType, string value, int ordering)
            => [TmdbNumber, entityType, entityID, TmdbNumber, language, languageCode, countryCode, (int)titleType, value, 1, (int)TextPreference.None, ordering];
    }

    #endregion

    #region TMDB Data | References

    /// <summary>
    ///   Records on TMDB's episode links the season and the numbers of the
    ///   episode each names, which the links of a source outside the core
    ///   carry, as TMDB's own tables gave them before.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> FillTmdbEpisodeLinkNumbers(object connection)
        => RunTmdbDataCopy(connection, "episode link numbers", "CrossRef_AniDB_Metadata_Episode", (transaction, report) =>
        {
            var episodes = new Dictionary<string, (int SeasonID, int SeasonNumber, int EpisodeNumber)>(StringComparer.Ordinal);
            foreach (var row in Read(transaction, "SELECT TmdbEpisodeID, TmdbSeasonID, SeasonNumber, EpisodeNumber FROM TMDB_Episode"))
                episodes.TryAdd(Text(AsInt(row[0])), (AsInt(row[1]), AsInt(row[2]), AsInt(row[3])));

            var links = Read(transaction, $"SELECT CrossRef_AniDB_Metadata_EpisodeID, ProviderID FROM CrossRef_AniDB_Metadata_Episode WHERE Source = {TmdbNumber}")
                .Select(row => (ID: AsInt(row[0]), Episode: row[1] as string ?? string.Empty))
                .Where(link => episodes.ContainsKey(link.Episode))
                .ToList();
            foreach (var chunk in links.Chunk(TmdbIDsPerList / 2))
            {
                string Case(Func<(int SeasonID, int SeasonNumber, int EpisodeNumber), string> value)
                    => $"CASE CrossRef_AniDB_Metadata_EpisodeID {string.Join(" ", chunk.Select(link => $"WHEN {link.ID} THEN {value(episodes[link.Episode])}"))} END";

                Execute(transaction,
                    $"UPDATE CrossRef_AniDB_Metadata_Episode SET ProviderSeasonID = {Case(episode => episode.SeasonID > 0 ? $"'{episode.SeasonID}'" : "NULL")}, " +
                    $"SeasonNumber = {Case(episode => Text(episode.SeasonNumber))}, EpisodeNumber = {Case(episode => Text(episode.EpisodeNumber))} " +
                    $"WHERE CrossRef_AniDB_Metadata_EpisodeID IN ({string.Join(", ", chunk.Select(link => link.ID))})");
            }

            return links.Count;
        });

    /// <summary>
    ///   Rewrites every preferred ordering that names a TMDB show's default
    ///   ordering by the show's own ID, <c>tmdb://ordering/123</c>, to the
    ///   default ordering ID of a source outside the core,
    ///   <c>tmdb://ordering/default/123</c>, on Shoko series, AniDB anime and
    ///   the series of every source.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> RewriteTmdbDefaultOrderingIDs(object connection)
        => RunTmdbDataCopy(connection, "default ordering choices", "the series tables", (transaction, report) =>
        {
            var rewritten = 0;
            foreach (var (table, idColumn) in (ReadOnlySpan<(string, string)>)[("AnimeSeries", "AnimeSeriesID"), ("AniDB_Anime", "AniDB_AnimeID"), ("Metadata_Series", "Metadata_SeriesID")])
            {
                foreach (var row in Read(transaction, $"SELECT {idColumn}, PreferredOrderingID FROM {table} WHERE PreferredOrderingID LIKE '{TmdbCoreOrderingPrefix}%'"))
                {
                    if (RewriteTmdbCoreOrdering(row[1] as string) is not { } value || value == row[1] as string)
                        continue;

                    using var command = transaction.Connection!.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = $"UPDATE {table} SET PreferredOrderingID = @value WHERE {idColumn} = {AsInt(row[0])}";
                    AddParameter(command, "@value", value);
                    command.ExecuteNonQuery();
                    rewritten++;
                }
            }

            return rewritten;
        });

    /// <summary>
    ///   Pins the image each TMDB show, season, episode and movie used as its
    ///   default of a type in the entry's extra data, leaving the images in
    ///   the order they were linked.
    /// </summary>
    /// <remarks>
    ///   The default was the poster and backdrop paths on a show's or movie's
    ///   row, the poster path on a season's and the still on an episode's.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyTmdbDefaultImages(object connection)
        => RunTmdbDataCopy(connection, "default images", "the entries' extra data", (transaction, report) =>
            CopyTmdbDefaultImages<Metadata_SeriesExtra>(transaction, "TMDB_Show", "TmdbShowID", "Metadata_Series",
                ("PosterPath", ImageEntityType.Primary), ("BackdropPath", ImageEntityType.Backdrop)) +
            CopyTmdbDefaultImages<Metadata_SeasonExtra>(transaction, "TMDB_Season", "TmdbSeasonID", "Metadata_Season", ("PosterPath", ImageEntityType.Primary)) +
            CopyTmdbDefaultImages<Metadata_EpisodeExtra>(transaction, "TMDB_Episode", "TmdbEpisodeID", "Metadata_Episode", ("ThumbnailPath", ImageEntityType.Backdrop)) +
            CopyTmdbDefaultImages<Metadata_MovieExtra>(transaction, "TMDB_Movie", "TmdbMovieID", "Metadata_Movie",
                ("PosterPath", ImageEntityType.Primary), ("BackdropPath", ImageEntityType.Backdrop)));

    /// <summary>
    ///   Pins the defaults the rows of one TMDB table named in the extra data
    ///   of the entries copied from them.
    /// </summary>
    /// <typeparam name="TExtra">The kind of extra data the entries keep.</typeparam>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The TMDB table.</param>
    /// <param name="idColumn">The TMDB table's column with TMDB's ID.</param>
    /// <param name="target">The shared table the entries were copied into.</param>
    /// <param name="paths">The TMDB table's image path columns, with the type of each.</param>
    /// <returns>How many entries it pinned defaults on.</returns>
    private static int CopyTmdbDefaultImages<TExtra>(
        DbTransaction transaction,
        string table,
        string idColumn,
        string target,
        params (string Column, ImageEntityType Type)[] paths
    )
        where TExtra : class, IMetadataDefaultImages<TExtra>, new()
    {
        var defaults = new Dictionary<string, Dictionary<ImageEntityType, string>>(StringComparer.Ordinal);
        foreach (var row in Read(transaction, $"SELECT {idColumn}, {string.Join(", ", paths.Select(path => path.Column))} FROM {table}"))
        {
            var resourceIDs = new Dictionary<ImageEntityType, string>();
            for (var index = 0; index < paths.Length; index++)
                if (NullIfEmpty(row[index + 1]) is { Length: > 1 } path)
                    resourceIDs[paths[index].Type] = DefaultImageResourceID(path);
            if (resourceIDs.Count > 0)
                defaults.TryAdd(Text(AsInt(row[0])), resourceIDs);
        }

        var converter = new JsonObjectConverter<TExtra>();
        var pinned = 0;
        foreach (var row in Read(transaction, $"SELECT {target}ID, ProviderID, ExtraData FROM {target} WHERE Source = {TmdbNumber}"))
        {
            if (row[1] is not string providerID || !defaults.TryGetValue(providerID, out var resourceIDs))
                continue;

            var extra = (converter.ConvertFrom(null, null, row[2] as string) as TExtra ?? new TExtra()).WithDefaultResourceIDs(resourceIDs);
            using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"UPDATE {target} SET ExtraData = @value WHERE {target}ID = {AsInt(row[0])}";
            AddParameter(command, "@value", ToExtraJson(extra));
            command.ExecuteNonQuery();
            pinned++;
        }

        return pinned;
    }

    /// <summary>
    ///   The resource ID an image is stored under for TMDB's path: the path
    ///   without its leading slash, with an SVG asked for as a PNG.
    /// </summary>
    /// <param name="path">TMDB's path for the image.</param>
    /// <returns>The resource ID.</returns>
    private static string DefaultImageResourceID(string path)
        => path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? path[1..^4] + ".png" : path[1..];

    /// <summary>
    ///   A preferred ordering ID, with TMDB's old default ordering ID for a
    ///   show rewritten to the one TMDB's default orderings have now.
    /// </summary>
    /// <param name="orderingID">The stored ordering ID, or <c>null</c>.</param>
    /// <returns>The ordering ID to store.</returns>
    private static string? RewriteTmdbCoreOrdering(string? orderingID)
    {
        if (string.IsNullOrEmpty(orderingID) || !orderingID.StartsWith(TmdbCoreOrderingPrefix, StringComparison.Ordinal))
            return string.IsNullOrEmpty(orderingID) ? null : orderingID;

        var id = orderingID[TmdbCoreOrderingPrefix.Length..];
        return id.Length > 0 && id.All(char.IsAsciiDigit) ? $"{TmdbCoreOrderingPrefix}default/{id}" : orderingID;
    }

    #endregion

    #region TMDB Data | Helpers

    /// <summary>
    ///   The stored number of the TMDB source.
    /// </summary>
    private static int TmdbNumber => MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);

    /// <summary>
    ///   The stored number of a kind of entry.
    /// </summary>
    /// <param name="entityType">The kind.</param>
    /// <returns>The number.</returns>
    private static int KindNumber(MetadataEntityType entityType)
        => MetadataNumberRegistry.GetNumber(entityType);

    /// <summary>
    ///   Runs one copy step in a transaction of its own, logging how many rows
    ///   it wrote.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <param name="what">What is copied, for the log.</param>
    /// <param name="target">The table copied into, for the log.</param>
    /// <param name="copy">Copies the rows, reporting its progress to the action it is given, and returns how many.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    private static Tuple<bool, string?> RunTmdbDataCopy(object connection, string what, string target, Func<DbTransaction, Action<string>, int> copy)
    {
        try
        {
            var dbConnection = (DbConnection)connection;
            if (dbConnection.State is not System.Data.ConnectionState.Open)
                dbConnection.Open();

            var started = DateTime.UtcNow;
            using var transaction = dbConnection.BeginTransaction();
            var count = copy(transaction, ReportToStartup());
            transaction.Commit();
            _logger.Info("Copied {Count} rows of TMDB {What} into {Table} in {Elapsed}.", count, what, target, DateTime.UtcNow - started);
            return new(true, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }

    /// <summary>
    ///   Reads a large table a page of entries at a time, so each entry's rows
    ///   come in one page.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The table.</param>
    /// <param name="keyColumn">The column naming the entry each row belongs to.</param>
    /// <param name="columns">The columns to read.</param>
    /// <param name="orderBy">The order of the rows within a page.</param>
    /// <param name="report">Called with each progress message.</param>
    /// <param name="label">The progress message.</param>
    /// <param name="keysPerPage">How many entries a page holds.</param>
    /// <returns>The pages, each read whole.</returns>
    private static IEnumerable<List<object?[]>> ReadTmdbPages(
        DbTransaction transaction,
        string table,
        string keyColumn,
        string columns,
        string orderBy,
        Action<string> report,
        string label,
        int keysPerPage = TmdbKeysPerPage
    )
    {
        var keys = ReadIDs(transaction, $"SELECT DISTINCT {keyColumn} FROM {table} ORDER BY {keyColumn}");
        var progress = new StartupProgress(report, label, keys.Count);
        foreach (var page in keys.Chunk(keysPerPage))
        {
            yield return Read(transaction, $"SELECT {columns} FROM {table} WHERE {keyColumn} BETWEEN {page[0]} AND {page[^1]} ORDER BY {orderBy}");
            progress.Advance(page.Length);
        }
    }

    /// <summary>
    ///   Inserts rows, many per statement on the servers and one prepared
    ///   statement per row on SQLite.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The table.</param>
    /// <param name="columns">The columns, in the order the rows give their values.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>How many rows it inserted.</returns>
    private static int TmdbInsert(DbTransaction transaction, string table, IReadOnlyList<string> columns, IEnumerable<object?[]> rows)
    {
        var connection = transaction.Connection!;
        var columnList = string.Join(", ", columns);
        var count = 0;
        if (connection is SqliteConnection)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO {table} ({columnList}) VALUES ({string.Join(", ", columns.Select((_, index) => $"@p{index}"))})";
            for (var index = 0; index < columns.Count; index++)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@p{index}";
                command.Parameters.Add(parameter);
            }

            command.Prepare();
            foreach (var row in rows)
            {
                for (var index = 0; index < columns.Count; index++)
                    command.Parameters[index].Value = ToParameterValue(row[index]);
                command.ExecuteNonQuery();
                count++;
            }

            return count;
        }

        var rowsPerInsert = Math.Max(1, TmdbParametersPerInsert / columns.Count);
        foreach (var chunk in rows.Chunk(rowsPerInsert))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var sql = new StringBuilder($"INSERT INTO {table} ({columnList}) VALUES ");
            for (var rowIndex = 0; rowIndex < chunk.Length; rowIndex++)
            {
                sql.Append(rowIndex is 0 ? "(" : ", (");
                for (var index = 0; index < columns.Count; index++)
                {
                    var name = $"@p{rowIndex}_{index}";
                    sql.Append(index is 0 ? name : $", {name}");
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = name;
                    parameter.Value = ToParameterValue(chunk[rowIndex][index]);
                    command.Parameters.Add(parameter);
                }

                sql.Append(')');
            }

            command.CommandText = sql.ToString();
            command.ExecuteNonQuery();
            count += chunk.Length;
        }

        return count;
    }

    /// <summary>
    ///   A value as a parameter takes it: a database null for <c>null</c>,
    ///   and <c>1</c> or <c>0</c> for a flag.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The parameter value.</returns>
    private static object ToParameterValue(object? value)
        => value switch
        {
            null => DBNull.Value,
            bool flag => flag ? 1 : 0,
            _ => value,
        };

    /// <summary>
    ///   The row ID of each TMDB row of a store table, by TMDB's ID for it.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The store table.</param>
    /// <param name="idColumn">The table's row ID column.</param>
    /// <returns>The row IDs, by provider ID.</returns>
    private static Dictionary<string, int> ReadTmdbRowIDs(DbTransaction transaction, string table, string idColumn)
        => Read(transaction, $"SELECT ProviderID, {idColumn} FROM {table} WHERE Source = {TmdbNumber}")
            .ToDictionary(row => (string)row[0]!, row => AsInt(row[1]), StringComparer.Ordinal);

    /// <summary>
    ///   The provider IDs of the TMDB rows of a store table.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The store table.</param>
    /// <returns>The provider IDs.</returns>
    private static HashSet<string> ReadTmdbProviderIDs(DbTransaction transaction, string table)
        => Read(transaction, $"SELECT ProviderID FROM {table} WHERE Source = {TmdbNumber}")
            .Select(row => (string)row[0]!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    ///   The original language of each TMDB show or movie, as TMDB gives it,
    ///   which is the language of its credits.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The show or movie table.</param>
    /// <param name="idColumn">The column holding the TMDB ID.</param>
    /// <returns>The language codes, by TMDB ID, for the entries that have one.</returns>
    private static Dictionary<int, string?> ReadTmdbLanguages(DbTransaction transaction, string table, string idColumn)
    {
        var languages = new Dictionary<int, string?>();
        foreach (var row in Read(transaction, $"SELECT {idColumn}, OriginalLanguageCode FROM {table}"))
            if (NullIfEmpty(row[1]) is { } code)
                languages.TryAdd(AsInt(row[0]), code.Trim());
        return languages;
    }

    /// <summary>
    ///   A provisional tag ID for a TMDB genre or keyword known only by name,
    ///   with a long name hashed to fit.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="kind">Whether it is a genre or a keyword.</param>
    /// <returns>The ID.</returns>
    private static string ProvisionalTmdbTagID(string name, TagKind kind)
    {
        var prefix = kind is TagKind.Genre ? TmdbGenrePrefix : TmdbKeywordPrefix;
        var id = prefix + name;
        return id.Length <= MetadataGuid.MaxIDLength
            ? id
            : prefix + "#" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
    }

    /// <summary>
    ///   An ID another source gave an entry.
    /// </summary>
    /// <param name="source">The other source's value.</param>
    /// <param name="entityType">The kind of entry.</param>
    /// <param name="id">The other source's ID.</param>
    /// <returns>The ID.</returns>
    private static MetadataGuid CrossSource(string source, MetadataEntityType entityType, string id)
        => new(MetadataSource.Parse(source), entityType, id);

    /// <summary>
    ///   The cross-source IDs column of a row, as the store writes it.
    /// </summary>
    /// <param name="ids">The IDs.</param>
    /// <returns>The column's value, or <c>null</c> when there are none.</returns>
    private static string? ToJson(List<MetadataGuid> ids)
        => ids.Count is 0 ? null : (string?)new JsonListConverter<MetadataGuid>().ConvertTo(null, null, ids, typeof(string));

    /// <summary>
    ///   The extra data column of a row, as the store writes it.
    /// </summary>
    /// <param name="extra">The extra data, or <c>null</c> when there is none.</param>
    /// <returns>The column's value, or <c>null</c>.</returns>
    private static string? ToExtraJson<T>(T? extra) where T : class
        => extra is null ? null : (string?)new JsonObjectConverter<T>().ConvertTo(null, null, extra, typeof(string));

    /// <summary>
    ///   The country codes of a row's production countries column, which
    ///   joins <c>code,name</c> pairs with <c>|</c>.
    /// </summary>
    /// <param name="value">The column's value.</param>
    /// <returns>The codes, each once, in their order.</returns>
    private static List<string> CountryCodes(object? value)
        => [
            .. (value as string ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(country => country.Split(',', 2, StringSplitOptions.TrimEntries)[0])
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

    /// <summary>
    ///   When an entry was last refreshed: its last update, unless it was
    ///   never updated since it was added.
    /// </summary>
    /// <param name="createdAt">When it was added, as read.</param>
    /// <param name="lastUpdatedAt">When it was last updated, as read.</param>
    /// <returns>The time, as read, or <c>null</c>.</returns>
    private static object? RefreshedAt(object? createdAt, object? lastUpdatedAt)
        => createdAt is null || lastUpdatedAt is null || Equals(createdAt, lastUpdatedAt) ? null : lastUpdatedAt;

    /// <summary>
    ///   An ID as text.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <returns>The text.</returns>
    private static string Text(int id)
        => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///   A value read as a number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The number, or <c>0</c> for <c>null</c>.</returns>
    private static int AsInt(object? value)
        => value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    /// <summary>
    ///   A value read as a decimal number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The number, or <c>0</c> for <c>null</c>.</returns>
    private static double AsDouble(object? value)
        => value is null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    /// <summary>
    ///   A value read as a flag, which each backend hands out in its own way.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The flag.</returns>
    private static bool AsBool(object? value)
        => value switch
        {
            null => false,
            bool flag => flag,
            string text => text is "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0,
        };

    /// <summary>
    ///   A value read as a day, which SQLite hands out as text.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The day, or <c>null</c>.</returns>
    private static DateOnly? AsDate(object? value)
        => value switch
        {
            DateTime date => DateOnly.FromDateTime(date),
            DateOnly date => date,
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => DateOnly.FromDateTime(date),
            _ => null,
        };

    /// <summary>
    ///   Whether every letter of a text is in the Latin script, as English
    ///   text is.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns><c>true</c> for Latin text, or text without letters.</returns>
    private static bool IsLatinText(string value)
        => value.All(character => !char.IsLetter(character) || character <= 'ɏ' || character is >= 'Ḁ' and <= 'ỿ');

    /// <summary>
    ///   A movie's IMDb ID, read from text, or from the number SQL Server's
    ///   column holds until its retyping fix has run.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The ID, or <c>null</c>.</returns>
    private static string? ImdbID(object? value)
        => value switch
        {
            null or string => NullIfEmpty(value),
            _ when AsInt(value) is > 0 and var number => "tt" + number.ToString("D7", CultureInfo.InvariantCulture),
            _ => null,
        };

    /// <summary>
    ///   A value read as text, unless it is missing or empty.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text, or <c>null</c>.</returns>
    private static string? NullIfEmpty(object? value)
        => value is string { Length: > 0 } text && !string.IsNullOrWhiteSpace(text) ? text : null;

    #endregion
}
