using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Services.Ordering;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
namespace Shoko.Server.API.v3.Helpers;

/// <summary>
///   Reads TMDB's entries from the shared metadata tables in the shape the
///   APIv3 TMDB routes and models always gave them, so their output stays the
///   same whoever writes the entries.
/// </summary>
public static class TmdbCompatibility
{
    #region Constants

    /// <summary>
    ///   The value of the source whose series and episode IDs TMDB lists among
    ///   an entry's cross-source IDs.
    /// </summary>
    internal const string SeriesCrossSource = "tvdb";

    /// <summary>
    ///   The value of the source whose movie IDs TMDB lists among a movie's
    ///   cross-source IDs.
    /// </summary>
    internal const string MovieCrossSource = "imdb";

    /// <summary>
    ///   The name the API gives a show's own ordering of its seasons.
    /// </summary>
    internal const string DefaultOrderingName = "Seasons";

    /// <summary>
    ///   The notes on a guest star's credit.
    /// </summary>
    internal const string GuestStarNotes = "Guest star";

    #endregion

    #region Services

    /// <summary>
    ///   A server service, for the models, which are not built through
    ///   dependency injection.
    /// </summary>
    /// <typeparam name="T">The service.</typeparam>
    /// <returns>The service.</returns>
    private static T Service<T>() where T : notnull
        => ISystemService.StaticServices.GetRequiredService<T>();

    #endregion

    #region Lookups

    /// <summary>
    ///   Looks up a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB's ID for the show.</param>
    /// <returns>The show, or <c>null</c> when it is not stored.</returns>
    public static Metadata_Series? GetShow(int showID)
        => showID <= 0 ? null : RepoFactory.Metadata_Series.GetByProviderID(MetadataSource.TMDB, Text(showID));

    /// <summary>
    ///   Looks up the TMDB show a link or ID names.
    /// </summary>
    /// <param name="showID">The show's ID, or <c>null</c>.</param>
    /// <returns>The show, or <c>null</c> when it names no stored TMDB show.</returns>
    public static Metadata_Series? GetShow(MetadataGuid? showID)
        => showID is not null && showID.Source == MetadataSource.TMDB && showID.EntityType == MetadataEntityType.Series
            ? RepoFactory.Metadata_Series.GetByProviderID(showID.Source, showID.ID)
            : null;

    /// <summary>
    ///   Looks up a TMDB season.
    /// </summary>
    /// <param name="seasonID">TMDB's ID for the season.</param>
    /// <returns>The season, or <c>null</c> when it is not stored.</returns>
    public static Metadata_Season? GetSeason(int seasonID)
        => seasonID <= 0 ? null : RepoFactory.Metadata_Season.GetByProviderID(MetadataSource.TMDB, Text(seasonID));

    /// <summary>
    ///   Looks up a TMDB episode.
    /// </summary>
    /// <param name="episodeID">TMDB's ID for the episode.</param>
    /// <returns>The episode, or <c>null</c> when it is not stored.</returns>
    public static Metadata_Episode? GetEpisode(int episodeID)
        => episodeID <= 0 ? null : RepoFactory.Metadata_Episode.GetByProviderID(MetadataSource.TMDB, Text(episodeID));

    /// <summary>
    ///   Looks up the TMDB episode an ID names.
    /// </summary>
    /// <param name="episodeID">The episode's ID, or <c>null</c>.</param>
    /// <returns>The episode, or <c>null</c> when it names no stored TMDB episode.</returns>
    public static Metadata_Episode? GetEpisode(MetadataGuid? episodeID)
        => episodeID is not null && episodeID.Source == MetadataSource.TMDB && episodeID.EntityType == MetadataEntityType.Episode
            ? RepoFactory.Metadata_Episode.GetByProviderID(episodeID.Source, episodeID.ID)
            : null;

    /// <summary>
    ///   Looks up a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB's ID for the movie.</param>
    /// <returns>The movie, or <c>null</c> when it is not stored.</returns>
    public static Metadata_Movie? GetMovie(int movieID)
        => movieID <= 0 ? null : RepoFactory.Metadata_Movie.GetByProviderID(MetadataSource.TMDB, Text(movieID));

    /// <summary>
    ///   Looks up the TMDB movie a link or ID names.
    /// </summary>
    /// <param name="movieID">The movie's ID, or <c>null</c>.</param>
    /// <returns>The movie, or <c>null</c> when it names no stored TMDB movie.</returns>
    public static Metadata_Movie? GetMovie(MetadataGuid? movieID)
        => movieID is not null && movieID.Source == MetadataSource.TMDB && movieID.EntityType == MetadataEntityType.Movie
            ? RepoFactory.Metadata_Movie.GetByProviderID(movieID.Source, movieID.ID)
            : null;

    /// <summary>
    ///   Looks up a TMDB movie collection.
    /// </summary>
    /// <param name="collectionID">TMDB's ID for the collection.</param>
    /// <returns>The collection, or <c>null</c> when it is not stored.</returns>
    public static Metadata_Collection? GetCollection(int collectionID)
        => collectionID <= 0 ? null : RepoFactory.Metadata_Collection.GetByProviderID(MetadataSource.TMDB, Text(collectionID));

    /// <summary>
    ///   Every stored TMDB show.
    /// </summary>
    /// <returns>The shows.</returns>
    public static IReadOnlyList<Metadata_Series> GetShows()
        => RepoFactory.Metadata_Series.GetBySource(MetadataSource.TMDB);

    /// <summary>
    ///   Every stored TMDB movie.
    /// </summary>
    /// <returns>The movies.</returns>
    public static IReadOnlyList<Metadata_Movie> GetMovies()
        => RepoFactory.Metadata_Movie.GetBySource(MetadataSource.TMDB);

    /// <summary>
    ///   Every stored TMDB movie collection.
    /// </summary>
    /// <returns>The collections.</returns>
    public static IReadOnlyList<Metadata_Collection> GetCollections()
        => RepoFactory.Metadata_Collection.GetBySource(MetadataSource.TMDB);

    /// <summary>
    ///   Looks up one of TMDB's alternate orderings, an episode group
    ///   collection.
    /// </summary>
    /// <param name="orderingID">TMDB's ID for the episode group collection.</param>
    /// <returns>The ordering, or <c>null</c> when it is not stored.</returns>
    public static AlternateOrdering? GetAlternateOrdering(string? orderingID)
        => !IsValidID(orderingID)
            ? null
            : AlternateOrdering.From(Service<IMetadataOrderingService>().GetOrdering(new(MetadataSource.TMDB, MetadataEntityType.Ordering, orderingID)));

    /// <summary>
    ///   Looks up a group of one of TMDB's alternate orderings, an episode
    ///   group.
    /// </summary>
    /// <param name="groupID">TMDB's ID for the episode group.</param>
    /// <returns>The group, or <c>null</c> when it is not stored.</returns>
    public static AlternateOrderingSeason? GetAlternateOrderingSeason(string? groupID)
    {
        if (!IsValidID(groupID) ||
            Service<Metadata_Ordering_GroupRepository>().GetByProviderID(MetadataSource.TMDB, groupID) is not { } group)
            return null;

        return GetAlternateOrdering(group.OrderingID)?.Seasons.FirstOrDefault(season => season.TmdbEpisodeGroupID == groupID);
    }

    /// <summary>
    ///   Looks up where an episode is placed in one of TMDB's alternate
    ///   orderings.
    /// </summary>
    /// <param name="orderingID">TMDB's ID for the episode group collection.</param>
    /// <param name="episodeID">TMDB's ID for the episode.</param>
    /// <param name="includeSpecialsInSeasons">
    ///   Whether a regular group lists the specials it holds too, see
    ///   <see cref="AlternateOrdering.GetEpisodes"/>.
    /// </param>
    /// <returns>The place, or <c>null</c> when the ordering does not place the episode.</returns>
    public static AlternateOrderingEpisode? GetAlternateOrderingEpisode(string? orderingID, int episodeID, bool includeSpecialsInSeasons = true)
        => GetEpisode(episodeID)?.GetTmdbAlternateOrderingEpisodeById(orderingID, includeSpecialsInSeasons);

    #endregion

    #region Cross-References

    /// <summary>
    ///   An anime's links to TMDB shows.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The links, in their order.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetShowLinks(int anidbAnimeID)
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Series.GetByAnidbAnimeID(anidbAnimeID, MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Show(row))];

    /// <summary>
    ///   The links from AniDB anime to a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB's ID for the show.</param>
    /// <returns>The links.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetShowLinksTo(int showID)
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Series.GetByProviderID(MetadataSource.TMDB, Text(showID)).Select(row => new CrossRef_AniDB_TMDB_Show(row))];

    /// <summary>
    ///   Every link from an AniDB anime to a TMDB show.
    /// </summary>
    /// <returns>The links.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetAllShowLinks()
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Series.GetAll().Where(row => row.Source == MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Show(row))];

    /// <summary>
    ///   An anime's links to TMDB movies.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The links, in their order.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetMovieLinks(int anidbAnimeID)
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByAnidbAnimeID(anidbAnimeID, MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    /// <summary>
    ///   An AniDB episode's links to TMDB movies.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <returns>The links, in their order.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetMovieLinksForEpisode(int anidbEpisodeID)
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByAnidbEpisodeID(anidbEpisodeID, MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    /// <summary>
    ///   The links from AniDB episodes to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB's ID for the movie.</param>
    /// <returns>The links.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetMovieLinksTo(int movieID)
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByProviderID(MetadataSource.TMDB, Text(movieID)).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    /// <summary>
    ///   Every link from an AniDB episode to a TMDB movie.
    /// </summary>
    /// <returns>The links.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetAllMovieLinks()
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Movie.GetAll().Where(row => row.Source == MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    /// <summary>
    ///   An anime's links to TMDB episodes.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="showID">Only the links into this TMDB show, if given.</param>
    /// <returns>The links.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetEpisodeLinks(int anidbAnimeID, int? showID = null)
        => [
            .. RepoFactory.CrossRef_AniDB_Metadata_Episode.GetByAnidbAnimeID(anidbAnimeID, MetadataSource.TMDB)
                .Select(row => new CrossRef_AniDB_TMDB_Episode(row))
                .Where(xref => showID is null || xref.TmdbShowID == showID),
        ];

    /// <summary>
    ///   An AniDB episode's links to TMDB episodes.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <returns>The links, in their order.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetEpisodeLinksForEpisode(int anidbEpisodeID)
        => [
            .. RepoFactory.CrossRef_AniDB_Metadata_Episode.GetByAnidbEpisodeID(anidbEpisodeID, MetadataSource.TMDB)
                .OrderBy(row => row.Ordering)
                .Select(row => new CrossRef_AniDB_TMDB_Episode(row)),
        ];

    /// <summary>
    ///   The links from AniDB episodes to a TMDB episode.
    /// </summary>
    /// <param name="episodeID">TMDB's ID for the episode.</param>
    /// <returns>The links, in their order.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetEpisodeLinksTo(int episodeID)
        => [
            .. RepoFactory.CrossRef_AniDB_Metadata_Episode.GetByProviderID(MetadataSource.TMDB, Text(episodeID))
                .OrderBy(row => row.Ordering)
                .Select(row => new CrossRef_AniDB_TMDB_Episode(row)),
        ];

    /// <summary>
    ///   The links from AniDB episodes into a TMDB show's episodes.
    /// </summary>
    /// <param name="showID">TMDB's ID for the show.</param>
    /// <returns>The links.</returns>
    public static IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetEpisodeLinksInto(int showID)
        => [.. RepoFactory.CrossRef_AniDB_Metadata_Episode.GetByProviderParentID(MetadataSource.TMDB, Text(showID)).Select(row => new CrossRef_AniDB_TMDB_Episode(row))];

    /// <summary>
    ///   The TMDB seasons some episode links reach, once each.
    /// </summary>
    /// <param name="links">The episode links.</param>
    /// <returns>The season links.</returns>
    private static IReadOnlyList<CrossRef_AniDB_TMDB_Season> SeasonLinksOf(IEnumerable<CrossRef_AniDB_TMDB_Episode> links)
        => [.. links.Select(xref => xref.TmdbSeasonCrossReference).WhereNotNull().Distinct()];

    extension(AniDB_Anime anime)
    {
        /// <summary>
        ///   The anime's links to TMDB shows.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Show> TmdbShowCrossReferences => GetShowLinks(anime.AnimeID);

        /// <summary>
        ///   The TMDB shows the anime is linked to, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Series> TmdbShows => [.. anime.TmdbShowCrossReferences.Select(xref => xref.TmdbShow).WhereNotNull()];

        /// <summary>
        ///   The anime's links to TMDB episodes.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => GetEpisodeLinks(anime.AnimeID);

        /// <summary>
        ///   The anime's links to TMDB episodes, of one show when given.
        /// </summary>
        /// <param name="tmdbShowId">The TMDB show, or every show.</param>
        /// <returns>The links.</returns>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetTmdbEpisodeCrossReferences(int? tmdbShowId = null) => GetEpisodeLinks(anime.AnimeID, tmdbShowId);

        /// <summary>
        ///   The TMDB seasons the anime's episode links reach.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Season> TmdbSeasonCrossReferences => SeasonLinksOf(anime.TmdbEpisodeCrossReferences);

        /// <summary>
        ///   The TMDB seasons the anime's episode links reach, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Season> TmdbSeasons => [.. anime.TmdbSeasonCrossReferences.Select(xref => xref.TmdbSeason).WhereNotNull()];

        /// <summary>
        ///   The TMDB seasons the anime's episode links reach, of one show when given.
        /// </summary>
        /// <param name="tmdbShowId">The TMDB show, or every show.</param>
        /// <returns>The season links.</returns>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Season> GetTmdbSeasonCrossReferences(int? tmdbShowId = null) => SeasonLinksOf(GetEpisodeLinks(anime.AnimeID, tmdbShowId));

        /// <summary>
        ///   The anime's links to TMDB movies.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => GetMovieLinks(anime.AnimeID);

        /// <summary>
        ///   The TMDB movies the anime is linked to, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Movie> TmdbMovies => [.. anime.TmdbMovieCrossReferences.Select(xref => xref.TmdbMovie).WhereNotNull()];
    }

    extension(AnimeSeries series)
    {
        /// <summary>
        ///   The series' links to TMDB shows.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Show> TmdbShowCrossReferences => GetShowLinks(series.AniDB_ID);

        /// <summary>
        ///   The TMDB shows the series is linked to, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Series> TmdbShows => [.. series.TmdbShowCrossReferences.Select(xref => xref.TmdbShow).WhereNotNull()];

        /// <summary>
        ///   The series' links to TMDB episodes.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => GetEpisodeLinks(series.AniDB_ID);

        /// <summary>
        ///   The series' links to TMDB episodes, of one show when given.
        /// </summary>
        /// <param name="tmdbShowId">The TMDB show, or every show.</param>
        /// <returns>The links.</returns>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetTmdbEpisodeCrossReferences(int? tmdbShowId = null) => GetEpisodeLinks(series.AniDB_ID, tmdbShowId);

        /// <summary>
        ///   The TMDB seasons the series' episode links reach.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Season> TmdbSeasonCrossReferences => SeasonLinksOf(series.TmdbEpisodeCrossReferences);

        /// <summary>
        ///   The TMDB seasons the series' episode links reach, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Season> TmdbSeasons => [.. series.TmdbSeasonCrossReferences.Select(xref => xref.TmdbSeason).WhereNotNull()];

        /// <summary>
        ///   The TMDB seasons the series' episode links reach, of one show when given.
        /// </summary>
        /// <param name="tmdbShowId">The TMDB show, or every show.</param>
        /// <returns>The season links.</returns>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Season> GetTmdbSeasonCrossReferences(int? tmdbShowId = null) => SeasonLinksOf(GetEpisodeLinks(series.AniDB_ID, tmdbShowId));

        /// <summary>
        ///   The series' links to TMDB movies.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => GetMovieLinks(series.AniDB_ID);

        /// <summary>
        ///   The TMDB movies the series is linked to, once each, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Movie> TmdbMovies
            => [.. series.TmdbMovieCrossReferences.DistinctBy(xref => xref.TmdbMovieID).Select(xref => xref.TmdbMovie).WhereNotNull()];
    }

    extension(AniDB_Episode episode)
    {
        /// <summary>
        ///   The episode's links to TMDB movies.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => GetMovieLinksForEpisode(episode.EpisodeID);

        /// <summary>
        ///   The TMDB movies the episode stands for, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Movie> TmdbMovies => [.. episode.TmdbMovieCrossReferences.Select(xref => xref.TmdbMovie).WhereNotNull()];

        /// <summary>
        ///   The episode's links to TMDB episodes.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => GetEpisodeLinksForEpisode(episode.EpisodeID);

        /// <summary>
        ///   The TMDB episodes the episode is linked to, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Episode> TmdbEpisodes => [.. episode.TmdbEpisodeCrossReferences.Select(xref => xref.TmdbEpisode).WhereNotNull()];
    }

    extension(AnimeEpisode episode)
    {
        /// <summary>
        ///   The episode's links to TMDB movies.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => GetMovieLinksForEpisode(episode.AniDB_EpisodeID);

        /// <summary>
        ///   The TMDB movies the episode stands for, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Movie> TmdbMovies => [.. episode.TmdbMovieCrossReferences.Select(xref => xref.TmdbMovie).WhereNotNull()];

        /// <summary>
        ///   The episode's links to TMDB episodes.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => GetEpisodeLinksForEpisode(episode.AniDB_EpisodeID);

        /// <summary>
        ///   The TMDB episodes the episode is linked to, as stored.
        /// </summary>
        public IReadOnlyList<Metadata_Episode> TmdbEpisodes => [.. episode.TmdbEpisodeCrossReferences.Select(xref => xref.TmdbEpisode).WhereNotNull()];
    }

    #endregion

    #region Shows

    extension(Metadata_Series show)
    {
        /// <summary>
        ///   TMDB's ID for the show.
        /// </summary>
        public int Id => Number(show.ProviderID);

        /// <summary>
        ///   TMDB's ID for the show.
        /// </summary>
        public int TmdbShowID => Number(show.ProviderID);

        /// <summary>
        ///   The ID another source gave the show, as TMDB listed it.
        /// </summary>
        public int? TvdbShowID => CrossSourceNumber(show, SeriesCrossSource, MetadataEntityType.Series);

        /// <summary>
        ///   The show's English title, as TMDB gave it.
        /// </summary>
        public string EnglishTitle => DefaultTitle(show);

        /// <summary>
        ///   The show's English overview, as TMDB gave it.
        /// </summary>
        public string EnglishOverview => DefaultOverview(show);

        /// <summary>
        ///   The show's title in the language it was first made in.
        /// </summary>
        public string OriginalTitle => OriginalTitleOf(show, show.OriginalLanguageCode);

        /// <summary>
        ///   The language the show was first made in, or an empty string.
        /// </summary>
        public string OriginalLanguageCodeOrEmpty => show.OriginalLanguageCode ?? string.Empty;

        /// <summary>
        ///   The day the show first aired.
        /// </summary>
        public DateOnly? FirstAiredAt => show.AirDate?.ToDateOnly();

        /// <summary>
        ///   The day the show last aired, once it has ended.
        /// </summary>
        public DateOnly? LastAiredAt => show.EndDate?.ToDateOnly();

        /// <summary>
        ///   The show's genres, each part of a combined genre on its own.
        /// </summary>
        public IReadOnlyList<string> Genres => GenresOf(show);

        /// <summary>
        ///   The show's keywords.
        /// </summary>
        public IReadOnlyList<string> Keywords => KeywordsOf(show);

        /// <summary>
        ///   The show's content ratings, by country.
        /// </summary>
        public IReadOnlyList<IContentRating> TmdbContentRatings => ByCountry(((IWithContentRatings)show).ContentRatings);

        /// <summary>
        ///   The countries the show was made in, by code, with their English
        ///   names.
        /// </summary>
        public IReadOnlyDictionary<string, string> TmdbProductionCountries => CountryNames(((ISeries)show).ProductionCountries);

        /// <summary>
        ///   The studios that made the show.
        /// </summary>
        public IReadOnlyList<IStudio> TmdbStudios => StudiosOf(show.ID);

        /// <summary>
        ///   The networks that aired the show.
        /// </summary>
        public IReadOnlyList<INetwork> TmdbNetworks => NetworksOf(show.ID);

        /// <summary>
        ///   The cast of the show, gathered from its episodes.
        /// </summary>
        public IReadOnlyList<ICast> TmdbCast => ((IWithCastAndCrew)show).Cast;

        /// <summary>
        ///   The crew of the show, gathered from its episodes.
        /// </summary>
        public IReadOnlyList<ICrew> TmdbCrew => ((IWithCastAndCrew)show).Crew;

        /// <summary>
        ///   The yearly seasons the show aired in.
        /// </summary>
        public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons => ((IWithYearlySeasons)show).YearlySeasons;

        /// <summary>
        ///   The show's seasons, the specials last.
        /// </summary>
        public IReadOnlyList<Metadata_Season> TmdbSeasons
            => [.. RepoFactory.Metadata_Season.GetBySeriesID(show.Source, show.ProviderID).OrderBy(season => season.SeasonNumber == 0).ThenBy(season => season.SeasonNumber)];

        /// <summary>
        ///   The show's episodes by season and number, the specials last.
        /// </summary>
        public IReadOnlyList<Metadata_Episode> TmdbEpisodes
            => [
                .. RepoFactory.Metadata_Episode.GetBySeriesID(show.Source, show.ProviderID)
                    .OrderBy(episode => episode.SeasonNumber == 0)
                    .ThenBy(episode => episode.SeasonNumber)
                    .ThenBy(episode => episode.EpisodeNumber),
            ];

        /// <summary>
        ///   How many of the show's episodes are shown, specials included.
        /// </summary>
        public int EpisodeCount => show.TmdbEpisodes.Count(episode => !episode.IsHidden);

        /// <summary>
        ///   How many of the show's episodes are hidden.
        /// </summary>
        public int HiddenEpisodeCount => show.TmdbEpisodes.Count(episode => episode.IsHidden);

        /// <summary>
        ///   How many seasons the show has, leaving out its specials.
        /// </summary>
        public int SeasonCount => show.TmdbSeasons.Count(season => season.SeasonNumber > 0);

        /// <summary>
        ///   TMDB's alternate orderings of the show.
        /// </summary>
        public IReadOnlyList<AlternateOrdering> TmdbAlternateOrdering
            => [.. Service<IMetadataOrderingService>().GetOrderings(show).Select(AlternateOrdering.From).WhereNotNull()];

        /// <summary>
        ///   How many alternate orderings TMDB has for the show.
        /// </summary>
        public int AlternateOrderingCount => show.TmdbAlternateOrdering.Count;

        /// <summary>
        ///   The alternate ordering chosen for the show, or <c>null</c> when
        ///   its own ordering or one of another source is in use.
        /// </summary>
        public AlternateOrdering? PreferredAlternateOrdering
            => AlternateOrdering.From(Service<IMetadataOrderingService>().GetPreferredOrdering(show));

        /// <summary>
        ///   TMDB's ID for the alternate ordering chosen for the show, or
        ///   <c>null</c> when its own ordering or one of another source is in
        ///   use.
        /// </summary>
        public string? PreferredAlternateOrderingID => show.PreferredAlternateOrdering?.TmdbEpisodeGroupCollectionID;

        /// <summary>
        ///   The links from AniDB anime to the show.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Show> CrossReferences => GetShowLinksTo(show.TmdbShowID);

        /// <summary>
        ///   The links from AniDB episodes to the show's episodes.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> EpisodeCrossReferences => GetEpisodeLinksInto(show.TmdbShowID);

        /// <summary>
        ///   The title to show for the show.
        /// </summary>
        /// <returns>The title.</returns>
        public ITitle GetPreferredTitle() => PreferredTitle(show);

        /// <summary>
        ///   Every title of the show.
        /// </summary>
        /// <returns>The titles.</returns>
        public IReadOnlyList<ITitle> GetAllTitles() => ((IWithTitles)show).Titles;

        /// <summary>
        ///   The overview to show for the show.
        /// </summary>
        /// <returns>The overview.</returns>
        public IText GetPreferredOverview() => PreferredOverview(show);

        /// <summary>
        ///   Every overview of the show.
        /// </summary>
        /// <returns>The overviews.</returns>
        public IReadOnlyList<IText> GetAllOverviews() => ((IWithOverviews)show).Overviews;
    }

    #endregion

    #region Seasons

    extension(Metadata_Season season)
    {
        /// <summary>
        ///   TMDB's ID for the season.
        /// </summary>
        public int TmdbSeasonID => Number(season.ProviderID);

        /// <summary>
        ///   TMDB's ID for the show the season belongs to.
        /// </summary>
        public int TmdbShowID => Number(season.SeriesID);

        /// <summary>
        ///   The season's English title, as TMDB gave it, or its generic name,
        ///   such as <c>Season 1</c>, which is never stored.
        /// </summary>
        public string EnglishTitle => season.GetEnglishTitle().Value;

        /// <summary>
        ///   The season's English title, as TMDB gave it, or its generic name.
        /// </summary>
        /// <returns>The title.</returns>
        public ITitle GetEnglishTitle()
            => ((IWithTitles)season).Titles.FirstOrDefault(title => title.Source == MetadataSource.TMDB && title.Type is TitleType.Main)
                ?? GroupTitle(GenericEpisodeTitles.SeasonName(season.SeasonNumber));

        /// <summary>
        ///   The season's English overview, as TMDB gave it.
        /// </summary>
        public string EnglishOverview => DefaultOverview(season);

        /// <summary>
        ///   The show the season belongs to.
        /// </summary>
        public Metadata_Series? TmdbShow => RepoFactory.Metadata_Series.GetByProviderID(season.Source, season.SeriesID);

        /// <summary>
        ///   The season's episodes by number.
        /// </summary>
        public IReadOnlyList<Metadata_Episode> TmdbEpisodes
            => [.. RepoFactory.Metadata_Episode.GetBySeasonID(season.Source, season.ProviderID).OrderBy(episode => episode.EpisodeNumber)];

        /// <summary>
        ///   How many of the season's episodes are shown.
        /// </summary>
        public int EpisodeCount => season.TmdbEpisodes.Count(episode => !episode.IsHidden);

        /// <summary>
        ///   How many of the season's episodes are hidden.
        /// </summary>
        public int HiddenEpisodeCount => season.TmdbEpisodes.Count(episode => episode.IsHidden);

        /// <summary>
        ///   The cast of the season, gathered from its episodes.
        /// </summary>
        public IReadOnlyList<ICast> TmdbCast => ((IWithCastAndCrew)season).Cast;

        /// <summary>
        ///   The crew of the season, gathered from its episodes.
        /// </summary>
        public IReadOnlyList<ICrew> TmdbCrew => ((IWithCastAndCrew)season).Crew;

        /// <summary>
        ///   The yearly seasons the season aired in.
        /// </summary>
        public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons => ((IWithYearlySeasons)season).YearlySeasons;

        /// <summary>
        ///   The title to show for the season: the one picked or preferred,
        ///   else its English title.
        /// </summary>
        /// <returns>The title.</returns>
        public ITitle GetPreferredTitle() => MetadataStoredEntry.PreferredTitle(season) ?? season.GetEnglishTitle();

        /// <summary>
        ///   Every title of the season, its generic name first when TMDB gave
        ///   it no English title.
        /// </summary>
        /// <returns>The titles.</returns>
        public IReadOnlyList<ITitle> GetAllTitles()
        {
            var titles = ((IWithTitles)season).Titles;
            return titles.Any(title => title.Source == MetadataSource.TMDB && title.Type is TitleType.Main) ? titles : [season.GetEnglishTitle(), .. titles];
        }

        /// <summary>
        ///   The overview to show for the season.
        /// </summary>
        /// <returns>The overview.</returns>
        public IText GetPreferredOverview() => PreferredOverview(season);

        /// <summary>
        ///   Every overview of the season.
        /// </summary>
        /// <returns>The overviews.</returns>
        public IReadOnlyList<IText> GetAllOverviews() => ((IWithOverviews)season).Overviews;
    }

    #endregion

    #region Episodes

    extension(Metadata_Episode episode)
    {
        /// <summary>
        ///   TMDB's ID for the episode.
        /// </summary>
        public int TmdbEpisodeID => Number(episode.ProviderID);

        /// <summary>
        ///   TMDB's ID for the season the episode belongs to, or <c>0</c>.
        /// </summary>
        public int TmdbSeasonID => Number(episode.SeasonID);

        /// <summary>
        ///   TMDB's ID for the show the episode belongs to.
        /// </summary>
        public int TmdbShowID => Number(episode.SeriesID);

        /// <summary>
        ///   The ID another source gave the episode, as TMDB listed it.
        /// </summary>
        public int? TvdbEpisodeID => CrossSourceNumber(episode, SeriesCrossSource, MetadataEntityType.Episode);

        /// <summary>
        ///   The episode's English title, as TMDB gave it, or an empty string
        ///   for a title such as <c>Episode 5</c>, which is never stored.
        /// </summary>
        public string EnglishTitle => episode.StoredEnglishTitle?.Value ?? string.Empty;

        /// <summary>
        ///   The English title TMDB gave the episode, or <c>null</c>.
        /// </summary>
        public ITitle? StoredEnglishTitle
            => ((IWithTitles)episode).Titles.FirstOrDefault(title => title.Source == MetadataSource.TMDB && title.Language is TitleLanguage.EnglishAmerican);

        /// <summary>
        ///   The episode's English overview, as TMDB gave it.
        /// </summary>
        public string EnglishOverview => DefaultOverview(episode);

        /// <summary>
        ///   The number of the season the episode is in.
        /// </summary>
        public int TmdbSeasonNumber => episode.SeasonNumber ?? 0;

        /// <summary>
        ///   The day the episode aired.
        /// </summary>
        public DateOnly? AiredAt => episode.AirDate;

        /// <summary>
        ///   How long the episode runs, when TMDB says.
        /// </summary>
        public TimeSpan? Runtime => episode.RuntimeSeconds > 0 ? TimeSpan.FromSeconds(episode.RuntimeSeconds) : null;

        /// <summary>
        ///   The season the episode belongs to.
        /// </summary>
        public Metadata_Season? TmdbSeason
            => string.IsNullOrEmpty(episode.SeasonID) ? null : RepoFactory.Metadata_Season.GetByProviderID(episode.Source, episode.SeasonID);

        /// <summary>
        ///   The show the episode belongs to.
        /// </summary>
        public Metadata_Series? TmdbShow => RepoFactory.Metadata_Series.GetByProviderID(episode.Source, episode.SeriesID);

        /// <summary>
        ///   The cast of the episode.
        /// </summary>
        public IReadOnlyList<ICast> TmdbCast => ((IWithCastAndCrew)episode).Cast;

        /// <summary>
        ///   The crew of the episode.
        /// </summary>
        public IReadOnlyList<ICrew> TmdbCrew => ((IWithCastAndCrew)episode).Crew;

        /// <summary>
        ///   Where TMDB's alternate orderings of the show place the episode,
        ///   by group ID.
        /// </summary>
        /// <param name="includeSpecialsInSeasons">
        ///   Whether a special is placed in the regular group holding it too,
        ///   see <see cref="AlternateOrdering.GetEpisodes"/>.
        /// </param>
        /// <returns>The places.</returns>
        public IReadOnlyList<AlternateOrderingEpisode> GetTmdbAlternateOrderingEpisodes(bool includeSpecialsInSeasons = true)
            => [
                .. Service<Metadata_Ordering_EntryRepository>().GetByEpisode(episode.ID)
                    .Where(entry => entry.Source == MetadataSource.TMDB)
                    .Select(entry => entry.OrderingID)
                    .Distinct(StringComparer.Ordinal)
                    .Select(GetAlternateOrdering)
                    .WhereNotNull()
                    .SelectMany(ordering => ordering.GetPlaces(includeSpecialsInSeasons).Where(place => place.EpisodeID == episode.ID))
                    .OrderBy(place => place.TmdbEpisodeGroupID, StringComparer.Ordinal),
            ];

        /// <summary>
        ///   The links from AniDB episodes to the episode.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> CrossReferences => GetEpisodeLinksTo(episode.TmdbEpisodeID);

        /// <summary>
        ///   The file links of the AniDB episodes linked to the episode.
        /// </summary>
        public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences
            => FileCrossReferencesOf(episode.CrossReferences.Select(xref => xref.AnidbEpisodeID));

        /// <summary>
        ///   Where one of TMDB's alternate orderings places the episode: in
        ///   the group with the lowest number holding it, so a special keeps
        ///   its number in the special group.
        /// </summary>
        /// <param name="orderingID">TMDB's ID for the episode group collection, or <c>null</c>.</param>
        /// <param name="includeSpecialsInSeasons">
        ///   Whether a regular group lists the specials it holds too, see
        ///   <see cref="AlternateOrdering.GetEpisodes"/>.
        /// </param>
        /// <returns>The place, or <c>null</c>.</returns>
        public AlternateOrderingEpisode? GetTmdbAlternateOrderingEpisodeById(string? orderingID, bool includeSpecialsInSeasons = true)
            => GetAlternateOrdering(orderingID)?.GetPlaces(includeSpecialsInSeasons)
                .Where(place => place.EpisodeID == episode.ID)
                .MinBy(place => place.SeasonNumber);

        /// <summary>
        ///   The episode's English title in an ordering: the one TMDB gave it,
        ///   else <c>Episode {number}</c> with its number in that ordering.
        /// </summary>
        /// <param name="place">Where an alternate ordering places the episode, or <c>null</c> for the default ordering.</param>
        /// <returns>The title.</returns>
        public ITitle GetEnglishTitle(AlternateOrderingEpisode? place = null)
            => episode.StoredEnglishTitle ?? GenericEpisodeTitle(place?.PlacedEpisodeNumber ?? episode.EpisodeNumber);

        /// <summary>
        ///   The title to show for the episode in an ordering: the one picked
        ///   or preferred, else its English title there.
        /// </summary>
        /// <param name="place">Where an alternate ordering places the episode, or <c>null</c> for the default ordering.</param>
        /// <returns>The title.</returns>
        public ITitle GetPreferredTitle(AlternateOrderingEpisode? place = null)
            => MetadataStoredEntry.PreferredTitle(episode) ?? episode.GetEnglishTitle(place);

        /// <summary>
        ///   Every title of the episode in an ordering, its English title there
        ///   first when TMDB gave it none.
        /// </summary>
        /// <param name="place">Where an alternate ordering places the episode, or <c>null</c> for the default ordering.</param>
        /// <returns>The titles.</returns>
        public IReadOnlyList<ITitle> GetAllTitles(AlternateOrderingEpisode? place = null)
        {
            var titles = ((IWithTitles)episode).Titles;
            return episode.StoredEnglishTitle is not null ? titles : [episode.GetEnglishTitle(place), .. titles];
        }

        /// <summary>
        ///   The episode's titles in an ordering, in the languages episodes are
        ///   named in.
        /// </summary>
        /// <param name="place">Where an alternate ordering places the episode, or <c>null</c> for the default ordering.</param>
        /// <returns>The titles.</returns>
        public IReadOnlyList<ITitle> GetAllPreferredTitles(AlternateOrderingEpisode? place = null)
        {
            var languages = Languages.PreferredEpisodeNamingLanguages.Select(language => language.Language).Append(TitleLanguage.English).ToHashSet();
            return [.. episode.GetAllTitles(place).WhereInLanguages(languages)];
        }

        /// <summary>
        ///   The overview to show for the episode.
        /// </summary>
        /// <returns>The overview.</returns>
        public IText GetPreferredOverview() => PreferredOverview(episode);

        /// <summary>
        ///   Every overview of the episode.
        /// </summary>
        /// <returns>The overviews.</returns>
        public IReadOnlyList<IText> GetAllOverviews() => ((IWithOverviews)episode).Overviews;
    }

    #endregion

    #region Movies

    extension(Metadata_Movie movie)
    {
        /// <summary>
        ///   TMDB's ID for the movie.
        /// </summary>
        public int Id => Number(movie.ProviderID);

        /// <summary>
        ///   TMDB's ID for the movie.
        /// </summary>
        public int TmdbMovieID => Number(movie.ProviderID);

        /// <summary>
        ///   IMDb's ID for the movie, as TMDB listed it.
        /// </summary>
        public string? ImdbMovieID
            => movie.CrossSourceIDs.FirstOrDefault(id => id.Source.Value == MovieCrossSource && id.EntityType == MetadataEntityType.Movie)?.ID;

        /// <summary>
        ///   The movie's English title, as TMDB gave it.
        /// </summary>
        public string EnglishTitle => DefaultTitle(movie);

        /// <summary>
        ///   The movie's English overview, as TMDB gave it.
        /// </summary>
        public string EnglishOverview => DefaultOverview(movie);

        /// <summary>
        ///   The movie's title in the language it was first made in.
        /// </summary>
        public string OriginalTitle => OriginalTitleOf(movie, movie.OriginalLanguageCode);

        /// <summary>
        ///   The language the movie was first made in, or an empty string.
        /// </summary>
        public string OriginalLanguageCodeOrEmpty => movie.OriginalLanguageCode ?? string.Empty;

        /// <summary>
        ///   How long the movie runs, when TMDB says.
        /// </summary>
        public TimeSpan? Runtime => movie.RuntimeSeconds is > 0 and var seconds ? TimeSpan.FromSeconds(seconds) : null;

        /// <summary>
        ///   The movie's genres, each part of a combined genre on its own.
        /// </summary>
        public IReadOnlyList<string> Genres => GenresOf(movie);

        /// <summary>
        ///   The movie's keywords.
        /// </summary>
        public IReadOnlyList<string> Keywords => KeywordsOf(movie);

        /// <summary>
        ///   The movie's content ratings, by country.
        /// </summary>
        public IReadOnlyList<IContentRating> TmdbContentRatings => ByCountry(((IWithContentRatings)movie).ContentRatings);

        /// <summary>
        ///   The countries the movie was made in, by code, with their English
        ///   names.
        /// </summary>
        public IReadOnlyDictionary<string, string> TmdbProductionCountries => CountryNames(((IMovie)movie).ProductionCountries);

        /// <summary>
        ///   The studios that made the movie.
        /// </summary>
        public IReadOnlyList<IStudio> TmdbStudios => StudiosOf(movie.ID);

        /// <summary>
        ///   The cast of the movie.
        /// </summary>
        public IReadOnlyList<ICast> TmdbCast => ((IWithCastAndCrew)movie).Cast;

        /// <summary>
        ///   The crew of the movie.
        /// </summary>
        public IReadOnlyList<ICrew> TmdbCrew => ((IWithCastAndCrew)movie).Crew;

        /// <summary>
        ///   The yearly seasons the movie came out in.
        /// </summary>
        public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons => ((IWithYearlySeasons)movie).YearlySeasons;

        /// <summary>
        ///   The collection the movie is in, when it is stored.
        /// </summary>
        public Metadata_Collection? TmdbCollection
            => movie.ExtraData?.CollectionID is { } collectionID ? RepoFactory.Metadata_Collection.GetByProviderID(movie.Source, collectionID) : null;

        /// <summary>
        ///   TMDB's ID for the collection the movie is in, stored or not.
        /// </summary>
        public int? TmdbCollectionID => movie.ExtraData?.CollectionID is { } collectionID ? Number(collectionID) : null;

        /// <summary>
        ///   The links from AniDB episodes to the movie.
        /// </summary>
        public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> CrossReferences => GetMovieLinksTo(movie.TmdbMovieID);

        /// <summary>
        ///   The file links of the AniDB episodes linked to the movie.
        /// </summary>
        public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences
            => FileCrossReferencesOf(movie.CrossReferences.Select(xref => xref.AnidbEpisodeID));

        /// <summary>
        ///   The title to show for the movie.
        /// </summary>
        /// <returns>The title.</returns>
        public ITitle GetPreferredTitle() => PreferredTitle(movie);

        /// <summary>
        ///   Every title of the movie.
        /// </summary>
        /// <returns>The titles.</returns>
        public IReadOnlyList<ITitle> GetAllTitles() => ((IWithTitles)movie).Titles;

        /// <summary>
        ///   The overview to show for the movie.
        /// </summary>
        /// <returns>The overview.</returns>
        public IText GetPreferredOverview() => PreferredOverview(movie);

        /// <summary>
        ///   Every overview of the movie.
        /// </summary>
        /// <returns>The overviews.</returns>
        public IReadOnlyList<IText> GetAllOverviews() => ((IWithOverviews)movie).Overviews;
    }

    #endregion

    #region Collections

    extension(Metadata_Collection collection)
    {
        /// <summary>
        ///   TMDB's ID for the collection.
        /// </summary>
        public int Id => Number(collection.ProviderID);

        /// <summary>
        ///   TMDB's ID for the collection.
        /// </summary>
        public int TmdbCollectionID => Number(collection.ProviderID);

        /// <summary>
        ///   The collection's English title, as TMDB gave it.
        /// </summary>
        public string EnglishTitle => DefaultTitle(collection);

        /// <summary>
        ///   The collection's English overview, as TMDB gave it.
        /// </summary>
        public string EnglishOverview => DefaultOverview(collection);

        /// <summary>
        ///   How many movies the collection holds.
        /// </summary>
        public int MovieCount => collection.Members.Count;

        /// <summary>
        ///   The stored movies of the collection, by English title.
        /// </summary>
        /// <returns>The movies.</returns>
        public IReadOnlyList<Metadata_Movie> GetTmdbMovies()
            => [
                .. collection.Members
                    .Where(member => member.EntityType == MetadataEntityType.Movie)
                    .Select(member => RepoFactory.Metadata_Movie.GetByProviderID(member.Source, member.ID))
                    .WhereNotNull()
                    .OrderBy(movie => movie.EnglishTitle)
                    .ThenBy(movie => movie.TmdbMovieID),
            ];

        /// <summary>
        ///   The title to show for the collection.
        /// </summary>
        /// <returns>The title.</returns>
        public ITitle GetPreferredTitle() => PreferredTitle(collection);

        /// <summary>
        ///   Every title of the collection.
        /// </summary>
        /// <returns>The titles.</returns>
        public IReadOnlyList<ITitle> GetAllTitles() => ((IWithTitles)collection).Titles;

        /// <summary>
        ///   The overview to show for the collection.
        /// </summary>
        /// <returns>The overview.</returns>
        public IText GetPreferredOverview() => PreferredOverview(collection);

        /// <summary>
        ///   Every overview of the collection.
        /// </summary>
        /// <returns>The overviews.</returns>
        public IReadOnlyList<IText> GetAllOverviews() => ((IWithOverviews)collection).Overviews;
    }

    #endregion

    #region Studios and Networks

    /// <summary>
    ///   TMDB's ID for a company or network.
    /// </summary>
    /// <param name="entry">The company or network.</param>
    /// <returns>The ID, or <c>0</c>.</returns>
    public static int TmdbID(IMetadata entry)
        => Number(entry.ID.ID);

    /// <summary>
    ///   How many shows and movies a company made, as TMDB linked them.
    /// </summary>
    /// <param name="studio">The company.</param>
    /// <returns>The count.</returns>
    public static int StudioSize(IStudio studio)
        => Service<IMetadataStudioStore>().GetEntriesForStudio(studio.ID).Count;

    /// <summary>
    ///   How many shows a network aired, as TMDB linked them, leaving out the
    ///   alternate orderings following it.
    /// </summary>
    /// <param name="network">The network.</param>
    /// <returns>The count.</returns>
    public static int NetworkSize(INetwork network)
        => Service<IMetadataStudioStore>().GetEntriesForNetwork(network.ID).Count(entry => entry.EntityType == MetadataEntityType.Series);

    /// <summary>
    ///   The companies that made an entry, leaving out those TMDB never sent.
    /// </summary>
    /// <param name="entry">The show or movie.</param>
    /// <returns>The companies, in order.</returns>
    private static IReadOnlyList<IStudio> StudiosOf(MetadataGuid entry)
        => [.. Service<IMetadataStudioStore>().GetStudios(entry).Where(studio => !MetadataModelBuilder.IsStub(studio))];

    /// <summary>
    ///   The networks that aired an entry, leaving out those TMDB never sent.
    /// </summary>
    /// <param name="entry">The show or ordering.</param>
    /// <returns>The networks, in order.</returns>
    private static IReadOnlyList<INetwork> NetworksOf(MetadataGuid entry)
        => [.. Service<IMetadataStudioStore>().GetNetworks(entry).Where(network => !MetadataModelBuilder.IsStub(network))];

    #endregion

    #region Suggestions

    /// <summary>
    ///   What TMDB suggests for a show or movie, or the suggestions TMDB made
    ///   that point at it, each with its place among the suggestions of its
    ///   kind in the list it is in, by kind and then place.
    /// </summary>
    /// <param name="entry">The show or movie.</param>
    /// <param name="reverse">Read the suggestions pointing at the entry instead.</param>
    /// <returns>The suggestions, with their places.</returns>
    public static IReadOnlyList<(ISuggestedMetadata Suggestion, int? Order)> GetSuggestions(MetadataGuid entry, bool reverse)
    {
        var store = Service<IMetadataSuggestionStore>();
        var suggestions = reverse ? store.GetSuggestedBy<IMetadata, IMetadata>(entry) : store.GetSuggestions<IMetadata, IMetadata>(entry);
        return
        [
            .. suggestions
                .Where(suggestion => suggestion.Source == MetadataSource.TMDB)
                .Select(suggestion => (Suggestion: (ISuggestedMetadata)suggestion, Order: suggestion.Order ?? PlaceAmongKind(store, suggestion)))
                .OrderBy(item => item.Suggestion.Kind)
                .ThenBy(item => item.Order ?? int.MaxValue),
        ];
    }

    /// <summary>
    ///   Where a suggestion sits among the suggestions of its kind its base
    ///   makes.
    /// </summary>
    /// <param name="store">The suggestion store.</param>
    /// <param name="suggestion">The suggestion.</param>
    /// <returns>The place, from <c>0</c>, or <c>null</c> when it is not found.</returns>
    private static int? PlaceAmongKind(IMetadataSuggestionStore store, ISuggestedMetadata suggestion)
    {
        var index = store.GetSuggestions<IMetadata, IMetadata>(suggestion.BaseID)
            .Where(other => other.Source == suggestion.Source && other.Kind == suggestion.Kind)
            .ToList()
            .FindIndex(other => other.Equals(suggestion));
        return index >= 0 ? index : null;
    }

    #endregion

    #region Credits

    /// <summary>
    ///   The person a credit names, or <c>null</c> when TMDB never sent the
    ///   person.
    /// </summary>
    /// <param name="creator">The credited creator, if any.</param>
    /// <returns>The person, or <c>null</c>.</returns>
    public static ICreator? CreditedPerson(ICreator? creator)
        => creator switch
        {
            null => null,
            Metadata_Creator { IsStub: true, Name.Length: 0 } => null,
            _ => creator,
        };

    /// <summary>
    ///   A person's English biography, as TMDB gave it, never one in another
    ///   language.
    /// </summary>
    /// <param name="person">The person.</param>
    /// <returns>The biography, or an empty string.</returns>
    public static string Biography(ICreator person)
        => (person as IInlineTextSource)?.InlineOverview?.Value ?? string.Empty;

    /// <summary>
    ///   Gathers the cast of some episodes as a show's or a season's: one
    ///   credit per person, character and guest role, by TMDB's ID for the
    ///   person, then by the credit's place in its episode.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <returns>The first credit of each person, character and guest role.</returns>
    public static IReadOnlyList<ICast> GatherCast(IEnumerable<Metadata_Episode> episodes)
        => [
            .. episodes
                .SelectMany(episode => episode.TmdbCast)
                .Where(cast => CreditedPerson(cast.Creator) is not null)
                .GroupBy(cast => (Person: cast.Creator!.ID, cast.Name, Guest: (cast as Metadata_Cast)?.RoleNotes == GuestStarNotes))
                .Select(group => group.First())
                .OrderBy(cast => Number(cast.Creator!.ID.ID))
                .ThenBy(cast => (cast as Metadata_Cast)?.Ordering ?? 0),
        ];

    /// <summary>
    ///   Gathers the crew of some episodes as a show's or a season's: one
    ///   credit per person, department and job, by TMDB's ID for the person,
    ///   then by job and department.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <returns>The first credit of each person, department and job.</returns>
    public static IReadOnlyList<ICrew> GatherCrew(IEnumerable<Metadata_Episode> episodes)
        => [
            .. episodes
                .SelectMany(episode => episode.TmdbCrew)
                .Where(crew => CreditedPerson(crew.Creator) is not null)
                .GroupBy(crew => (Person: crew.Creator!.ID, crew.Name))
                .Select(group => group.First())
                .OrderBy(crew => Number(crew.Creator!.ID.ID))
                .ThenBy(crew => DepartmentAndJob(crew).Job)
                .ThenBy(crew => DepartmentAndJob(crew).Department),
        ];

    /// <summary>
    ///   Splits a crew credit's name into TMDB's department and job.
    /// </summary>
    /// <param name="crew">The crew credit.</param>
    /// <returns>The department and the job.</returns>
    public static (string Department, string Job) DepartmentAndJob(ICrew crew)
    {
        var parts = crew.Name.Split(", ", 2);
        return parts.Length is 2 ? (parts[0], parts[1]) : (string.Empty, crew.Name);
    }

    #endregion

    #region Texts

    /// <summary>
    ///   The title TMDB gave an entry in English.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The title, or an empty string.</returns>
    public static string DefaultTitle(IWithTitles entry)
        => entry.DefaultTitle.Value;

    /// <summary>
    ///   The overview TMDB gave an entry in English.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The overview, or an empty string.</returns>
    public static string DefaultOverview(IWithOverviews entry)
        => EnglishOverview(entry)?.Value ?? string.Empty;

    /// <summary>
    ///   The American English overview TMDB gave an entry, the only one an
    ///   entry falls back to outside the preferred languages.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The overview, or <c>null</c> when TMDB gave none in English.</returns>
    private static IText? EnglishOverview(IWithOverviews entry)
        => entry.Overviews.FirstOrDefault(overview => overview.Source == MetadataSource.TMDB &&
            string.Equals(overview.LanguageCode, "en", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(overview.CountryCode, "US", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///   The title to show for an entry: the one picked or preferred, else
    ///   the one TMDB gave in English.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The title.</returns>
    public static ITitle PreferredTitle(IWithTitles entry)
        => entry.PreferredTitle ?? entry.DefaultTitle;

    /// <summary>
    ///   The overview to show for an entry: the one picked or preferred, else
    ///   the one TMDB gave in English, else an empty one, never one in another
    ///   language.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The overview.</returns>
    public static IText PreferredOverview(IWithOverviews entry)
        => entry.PreferredOverview ?? EnglishOverview(entry) ?? new TextStub
        {
            Source = MetadataSource.TMDB,
            Language = TitleLanguage.EnglishAmerican,
            LanguageCode = "en",
            CountryCode = "US",
            Value = string.Empty,
        };

    /// <summary>
    ///   An entry's title in the language it was first made in: the one
    ///   stored for that language alone, else any in that language, else its
    ///   English one.
    /// </summary>
    /// <param name="entry">The show or movie.</param>
    /// <param name="languageCode">The language it was first made in.</param>
    /// <returns>The title.</returns>
    private static string OriginalTitleOf(IWithTitles entry, string? languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return DefaultTitle(entry);

        var titles = entry.Titles.Where(title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase)).ToList();
        return (titles.FirstOrDefault(title => string.IsNullOrEmpty(title.CountryCode)) ?? titles.FirstOrDefault())?.Value ?? DefaultTitle(entry);
    }

    /// <summary>
    ///   The English title an episode TMDB gave none is shown with.
    /// </summary>
    /// <param name="number">The episode's number in the ordering shown.</param>
    /// <returns>The title.</returns>
    public static ITitle GenericEpisodeTitle(int number)
        => GroupTitle(string.Create(CultureInfo.InvariantCulture, $"Episode {number}"));

    /// <summary>
    ///   The title of a group of one of TMDB's alternate orderings: its name,
    ///   which TMDB gives in English.
    /// </summary>
    /// <param name="name">The group's name.</param>
    /// <returns>The title.</returns>
    public static ITitle GroupTitle(string name)
        => new TitleStub
        {
            Source = MetadataSource.TMDB,
            Language = TitleLanguage.EnglishAmerican,
            LanguageCode = "en",
            CountryCode = "US",
            Value = name,
            Type = TitleType.Main,
        };

    /// <summary>
    ///   The texts in some languages: those of a language, or of a language
    ///   and country.
    /// </summary>
    /// <typeparam name="TText">The kind of text.</typeparam>
    /// <param name="texts">The texts.</param>
    /// <param name="languages">The languages, or <c>null</c> for all.</param>
    /// <returns>The texts in the languages.</returns>
    public static IEnumerable<TText> WhereInLanguages<TText>(this IEnumerable<TText> texts, IReadOnlySet<TitleLanguage>? languages) where TText : IText
    {
        if (languages is null)
            return texts;

        var (languageCodes, countryCodes) = LanguageAndCountryCodes(languages);
        return texts.Where(text => languageCodes.Contains(text.LanguageCode) || (text.CountryCode is { } country && countryCodes.Contains(country)));
    }

    /// <summary>
    ///   The content ratings in some languages: those of a language, or of a
    ///   language and country.
    /// </summary>
    /// <param name="contentRatings">The content ratings.</param>
    /// <param name="languages">The languages, or <c>null</c> for all.</param>
    /// <returns>The content ratings in the languages.</returns>
    public static IEnumerable<IContentRating> WhereInLanguages(this IEnumerable<IContentRating> contentRatings, IReadOnlySet<TitleLanguage>? languages)
    {
        if (languages is null)
            return contentRatings;

        var (languageCodes, countryCodes) = LanguageAndCountryCodes(languages);
        return contentRatings.Where(rating => languageCodes.Contains(rating.LanguageCode) || countryCodes.Contains(rating.CountryCode));
    }

    /// <summary>
    ///   The language codes of the languages without a country, and the
    ///   country codes of those with one.
    /// </summary>
    /// <param name="languages">The languages.</param>
    /// <returns>The codes.</returns>
    private static (HashSet<string> LanguageCodes, HashSet<string> CountryCodes) LanguageAndCountryCodes(IReadOnlySet<TitleLanguage> languages)
    {
        var codes = languages.Select(language => language.GetLanguageAndCountryCode()).ToList();
        return (
            codes.Where(code => code.countryCode is null).Select(code => code.languageCode).ToHashSet(StringComparer.OrdinalIgnoreCase),
            codes.Where(code => code.countryCode is not null).Select(code => code.countryCode!).ToHashSet(StringComparer.OrdinalIgnoreCase)
        );
    }

    #endregion

    #region Tags and Countries

    /// <summary>
    ///   An entry's genres as TMDB's API gave them, in order. The source
    ///   stores each part of a combined genre as a genre of its own.
    /// </summary>
    /// <param name="entry">The show or movie.</param>
    /// <returns>The genres.</returns>
    private static IReadOnlyList<string> GenresOf(IWithTags entry)
        => [
            .. entry.Tags
                .Where(tag => tag.Kind is TagKind.Genre)
                .Select(tag => tag.Name)
                .OrderBy(genre => genre),
        ];

    /// <summary>
    ///   An entry's keywords, in order.
    /// </summary>
    /// <param name="entry">The show or movie.</param>
    /// <returns>The keywords.</returns>
    private static IReadOnlyList<string> KeywordsOf(IWithTags entry)
        => [.. entry.Tags.Where(tag => tag.Kind is TagKind.Keyword).Select(tag => tag.Name)];

    /// <summary>
    ///   Content ratings by country, those of one country in their order.
    /// </summary>
    /// <param name="contentRatings">The content ratings.</param>
    /// <returns>The content ratings, by country.</returns>
    internal static IReadOnlyList<IContentRating> ByCountry(IEnumerable<IContentRating> contentRatings)
        => [.. contentRatings.OrderBy(rating => rating.CountryCode, StringComparer.Ordinal)];

    /// <summary>
    ///   The English names of some countries, by code.
    /// </summary>
    /// <param name="countryCodes">The countries' codes.</param>
    /// <returns>The names, by code, or the code where no name is known.</returns>
    public static IReadOnlyDictionary<string, string> CountryNames(IEnumerable<string> countryCodes)
    {
        var names = new Dictionary<string, string>();
        foreach (var code in countryCodes)
            names.TryAdd(code, CountryName(code));
        return names;
    }

    /// <summary>
    ///   The English name of a country.
    /// </summary>
    /// <param name="countryCode">The country's code.</param>
    /// <returns>The name, or the code where no name is known.</returns>
    private static string CountryName(string countryCode)
    {
        try
        {
            return new RegionInfo(countryCode).EnglishName;
        }
        catch (ArgumentException)
        {
            return countryCode;
        }
    }

    #endregion

    #region Images

    /// <summary>
    ///   Where TMDB serves the image of one type an entry uses by default.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="imageType">The type of image.</param>
    /// <returns>The image's address, or <c>null</c> when the entry has none.</returns>
    public static string? DefaultImageUrl(IWithImages entry, ImageEntityType imageType)
        => entry.GetDefaultImageCrossReferenceForType(imageType)?.GetImage() is { } image &&
            Service<IImageManager>().GetTemplateUrlForSource(MetadataSource.TMDB) is { Length: > 0 } template
                ? string.Format(template, image.ResourceID)
                : null;

    #endregion

    #region Helpers

    /// <summary>
    ///   An ID as TMDB's number for it.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <returns>The number, or <c>0</c>.</returns>
    internal static int Number(string? id)
        => int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

    /// <summary>
    ///   TMDB's number as an ID.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The ID.</returns>
    internal static string Text(int number)
        => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///   The file links of some AniDB episodes.
    /// </summary>
    /// <param name="anidbEpisodeIDs">The AniDB episodes.</param>
    /// <returns>The file links.</returns>
    private static IReadOnlyList<CrossRef_File_Episode> FileCrossReferencesOf(IEnumerable<int> anidbEpisodeIDs)
        => [.. anidbEpisodeIDs.Distinct().SelectMany(RepoFactory.CrossRef_File_Episode.GetByEpisodeID)];

    /// <summary>
    ///   Whether some text can be the ID of a stored entry.
    /// </summary>
    /// <param name="id">The text.</param>
    /// <returns><c>true</c> when it can.</returns>
    private static bool IsValidID([NotNullWhen(true)] string? id)
        => !string.IsNullOrEmpty(id) && id.Length <= MetadataGuid.MaxIDLength && !char.IsWhiteSpace(id[0]) && !char.IsWhiteSpace(id[^1]);

    /// <summary>
    ///   The ID another source gave an entry, as a number.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="sourceValue">The other source's value.</param>
    /// <param name="entityType">The kind of entry.</param>
    /// <returns>The ID, or <c>null</c>.</returns>
    private static int? CrossSourceNumber(IWithCrossSources entry, string sourceValue, MetadataEntityType entityType)
        => entry.CrossSourceIDs.FirstOrDefault(id => id.Source.Value == sourceValue && id.EntityType == entityType) is { } crossSourceID &&
            Number(crossSourceID.ID) is > 0 and var number
                ? number
                : null;

    #endregion

    #region Alternate Orderings

    /// <summary>
    ///   One of TMDB's alternate orderings of a show, an episode group
    ///   collection.
    /// </summary>
    public sealed class AlternateOrdering
    {
        private IReadOnlyList<AlternateOrderingSeason>? _seasons;

        private IReadOnlyList<AlternateOrderingEpisode>? _listedPlaces;

        private IReadOnlyList<AlternateOrderingEpisode>? _placedPlaces;

        private AlternateOrdering(IOrdering ordering)
            => Ordering = ordering;

        /// <summary>
        ///   The ordering as the core serves it.
        /// </summary>
        public IOrdering Ordering { get; }

        /// <summary>
        ///   TMDB's ID for the episode group collection.
        /// </summary>
        public string TmdbEpisodeGroupCollectionID => Ordering.ID.ID;

        /// <summary>
        ///   TMDB's ID for the show it orders.
        /// </summary>
        public int TmdbShowID => Number(Ordering.SeriesID.ID);

        /// <summary>
        ///   The ordering's English name.
        /// </summary>
        public string EnglishTitle => Ordering.Name;

        /// <summary>
        ///   The ordering's English overview.
        /// </summary>
        public string EnglishOverview => Ordering.Overview;

        /// <summary>
        ///   What the ordering follows.
        /// </summary>
        public OrderingType Type => Ordering.Type;

        /// <summary>
        ///   The show it orders.
        /// </summary>
        public Metadata_Series? TmdbShow => GetShow(TmdbShowID);

        /// <summary>
        ///   The ordering's groups, as seasons, the specials last.
        /// </summary>
        public IReadOnlyList<AlternateOrderingSeason> Seasons
            => _seasons ??= [
                .. Ordering.Seasons
                    .Select(season => new AlternateOrderingSeason(this, season))
                    .OrderBy(season => season.SeasonNumber == 0)
                    .ThenBy(season => season.SeasonNumber),
            ];

        /// <summary>
        ///   Every episode's place in the ordering's groups, by season and
        ///   number, the specials last.
        /// </summary>
        /// <param name="includeSpecialsInSeasons">
        ///   List a special that the special group and a regular group both
        ///   hold in the regular group too, as TMDB does, every episode of
        ///   that group numbered by its place there. Else it is listed in the
        ///   special group only, and the regular group numbers its other
        ///   episodes without it.
        /// </param>
        /// <returns>The places.</returns>
        public IReadOnlyList<AlternateOrderingEpisode> GetEpisodes(bool includeSpecialsInSeasons = true)
            => [
                .. GetPlaces(includeSpecialsInSeasons)
                    .OrderBy(place => place.SeasonNumber == 0)
                    .ThenBy(place => place.SeasonNumber)
                    .ThenBy(place => place.PlacedEpisodeNumber),
            ];

        /// <summary>
        ///   Every episode's place in the ordering's groups, in the order of
        ///   the groups and of each group's episodes.
        /// </summary>
        /// <param name="includeSpecialsInSeasons">
        ///   Whether a special is listed in the regular group holding it too,
        ///   see <see cref="GetEpisodes"/>.
        /// </param>
        /// <returns>The places.</returns>
        internal IReadOnlyList<AlternateOrderingEpisode> GetPlaces(bool includeSpecialsInSeasons)
            => includeSpecialsInSeasons
                ? _listedPlaces ??= BuildPlaces(true)
                : _placedPlaces ??= BuildPlaces(false);

        /// <summary>
        ///   Builds every episode's place in the ordering's groups. A placed
        ///   special's place in the special group says where it airs, in the
        ///   numbers the regular groups are listed with.
        /// </summary>
        /// <param name="includeSpecialsInSeasons">Whether a special is listed in the regular group holding it too.</param>
        /// <returns>The places, in the order of the groups and of each group's episodes.</returns>
        private List<AlternateOrderingEpisode> BuildPlaces(bool includeSpecialsInSeasons)
        {
            var places = new List<AlternateOrderingEpisode>();
            if (Ordering is not IPlacedOrdering { Placement: var placement })
            {
                foreach (var season in Ordering.Seasons)
                    places.AddRange(season.Episodes.Select((episode, index) => new AlternateOrderingEpisode(this, season, episode.ID, index + 1, null)));
                return places;
            }

            var listedNumbers = new Dictionary<(MetadataGuid GroupID, MetadataGuid EpisodeID), int>();
            foreach (var season in Ordering.Seasons)
            {
                var listed = placement.Listed(season.ID);
                if (season.IsSpecial)
                {
                    places.AddRange(listed.Select(place => new AlternateOrderingEpisode(this, season, place.EpisodeID, place.EpisodeNumber, place.Airing)));
                }
                else if (!includeSpecialsInSeasons)
                {
                    places.AddRange(listed
                        .Where(place => !place.IsSpecial)
                        .Select(place => new AlternateOrderingEpisode(this, season, place.EpisodeID, place.EpisodeNumber, null)));
                }
                else
                {
                    foreach (var (index, place) in listed.Index())
                    {
                        listedNumbers.TryAdd((season.ID, place.EpisodeID), index + 1);
                        places.Add(new AlternateOrderingEpisode(this, season, place.EpisodeID, index + 1, null));
                    }
                }
            }

            if (!includeSpecialsInSeasons)
                return places;

            // A special airs in the first regular group listing it, before the episode of that group that follows it.
            for (var index = 0; index < places.Count; index++)
            {
                var special = places[index];
                var group = Ordering.Seasons.FirstOrDefault(season => !season.IsSpecial && listedNumbers.ContainsKey((season.ID, special.EpisodeID)));
                if (special.AirsBeforeEpisodeNumber is null || special.AirsBeforeEpisodeID is not { } nextID || group is null ||
                    !listedNumbers.TryGetValue((group.ID, nextID), out var nextNumber))
                    continue;

                places[index] = special.WithAirsBeforeEpisodeNumber(nextNumber);
            }

            return places;
        }

        /// <summary>
        ///   How many shown episodes the ordering's groups list, an episode
        ///   counted once for each group listing it.
        /// </summary>
        public int EpisodeCount => Seasons.Sum(season => season.EpisodeCount);

        /// <summary>
        ///   How many hidden episodes the ordering's groups list, an episode
        ///   counted once for each group listing it.
        /// </summary>
        public int HiddenEpisodeCount => Seasons.Sum(season => season.HiddenEpisodeCount);

        /// <summary>
        ///   How many groups the ordering has.
        /// </summary>
        public int SeasonCount => Seasons.Count;

        /// <summary>
        ///   The cast of the episodes the ordering places.
        /// </summary>
        public IReadOnlyList<ICast> Cast => GatherCast(PlacedEpisodes);

        /// <summary>
        ///   The crew of the episodes the ordering places.
        /// </summary>
        public IReadOnlyList<ICrew> Crew => GatherCrew(PlacedEpisodes);

        /// <summary>
        ///   When the ordering was first stored, in UTC.
        /// </summary>
        public DateTime CreatedAt => Ordering.CreatedAt;

        /// <summary>
        ///   When TMDB last changed the ordering, in UTC.
        /// </summary>
        public DateTime LastUpdatedAt => Ordering.LastUpdatedAt;

        /// <summary>
        ///   The stored episodes the ordering places, each once.
        /// </summary>
        private IReadOnlyList<Metadata_Episode> PlacedEpisodes
            => [.. Seasons.SelectMany(season => season.ListedEpisodes).DistinctBy(episode => episode.ProviderID)];

        /// <summary>
        ///   An ordering as one of TMDB's alternate orderings.
        /// </summary>
        /// <param name="ordering">The ordering.</param>
        /// <returns>The alternate ordering, or <c>null</c> when it is not one of TMDB's stored orderings.</returns>
        internal static AlternateOrdering? From(IOrdering? ordering)
            => ordering is { IsDefault: false } && ordering.ID.Source == MetadataSource.TMDB && ordering.SeriesID.Source == MetadataSource.TMDB
                ? new(ordering)
                : null;
    }

    /// <summary>
    ///   A group of one of TMDB's alternate orderings, an episode group.
    /// </summary>
    public sealed class AlternateOrderingSeason
    {
        internal AlternateOrderingSeason(AlternateOrdering ordering, ISeason season)
        {
            TmdbAlternateOrdering = ordering;
            Season = season;
        }

        /// <summary>
        ///   The ordering the group belongs to.
        /// </summary>
        public AlternateOrdering TmdbAlternateOrdering { get; }

        /// <summary>
        ///   The group as the core serves it.
        /// </summary>
        public ISeason Season { get; }

        /// <summary>
        ///   TMDB's ID for the episode group.
        /// </summary>
        public string TmdbEpisodeGroupID => Season.ID.ID;

        /// <summary>
        ///   TMDB's ID for the episode group collection.
        /// </summary>
        public string TmdbEpisodeGroupCollectionID => TmdbAlternateOrdering.TmdbEpisodeGroupCollectionID;

        /// <summary>
        ///   TMDB's ID for the show.
        /// </summary>
        public int TmdbShowID => TmdbAlternateOrdering.TmdbShowID;

        /// <summary>
        ///   The group's English name.
        /// </summary>
        public string EnglishTitle => Season.DefaultTitle.Value;

        /// <summary>
        ///   The group's number among the ordering's seasons.
        /// </summary>
        public int SeasonNumber => Season.SeasonNumber;

        /// <summary>
        ///   The show the group's ordering orders.
        /// </summary>
        public Metadata_Series? TmdbShow => TmdbAlternateOrdering.TmdbShow;

        /// <summary>
        ///   The stored episodes the group lists, in order.
        /// </summary>
        public IReadOnlyList<Metadata_Episode> ListedEpisodes
            => [
                .. Service<Metadata_Ordering_EntryRepository>().GetByOrderingID(MetadataSource.TMDB, TmdbEpisodeGroupCollectionID)
                    .Where(entry => entry.GroupID == TmdbEpisodeGroupID)
                    .OrderBy(entry => entry.Position)
                    .Select(entry => RepoFactory.Metadata_Episode.GetByProviderID(entry.EpisodeSource, entry.EpisodeID))
                    .WhereNotNull(),
            ];

        /// <summary>
        ///   The places of the episodes the group lists, in order.
        /// </summary>
        /// <param name="includeSpecialsInSeasons">
        ///   Whether a regular group lists the specials it holds too, see
        ///   <see cref="AlternateOrdering.GetEpisodes"/>.
        /// </param>
        /// <returns>The places.</returns>
        public IReadOnlyList<AlternateOrderingEpisode> GetEpisodes(bool includeSpecialsInSeasons = true)
            => [.. TmdbAlternateOrdering.GetPlaces(includeSpecialsInSeasons).Where(place => place.TmdbEpisodeGroupID == TmdbEpisodeGroupID)];

        /// <summary>
        ///   How many of the episodes the group lists are shown.
        /// </summary>
        public int EpisodeCount => ListedEpisodes.Count(episode => !episode.IsHidden);

        /// <summary>
        ///   How many of the episodes the group lists are hidden.
        /// </summary>
        public int HiddenEpisodeCount => ListedEpisodes.Count(episode => episode.IsHidden);

        /// <summary>
        ///   The cast of the episodes the group lists.
        /// </summary>
        public IReadOnlyList<ICast> Cast => GatherCast(ListedEpisodes);

        /// <summary>
        ///   The crew of the episodes the group lists.
        /// </summary>
        public IReadOnlyList<ICrew> Crew => GatherCrew(ListedEpisodes);

        /// <summary>
        ///   The yearly seasons the group's episodes aired in.
        /// </summary>
        public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons => Season.YearlySeasons;

        /// <summary>
        ///   When the group's ordering was first stored, in UTC.
        /// </summary>
        public DateTime CreatedAt => TmdbAlternateOrdering.CreatedAt;

        /// <summary>
        ///   When TMDB last changed the group's ordering, in UTC.
        /// </summary>
        public DateTime LastUpdatedAt => TmdbAlternateOrdering.LastUpdatedAt;

        /// <summary>
        ///   The group's title, its English name.
        /// </summary>
        /// <returns>The title.</returns>
        public ITitle GetPreferredTitle() => GroupTitle(EnglishTitle);

        /// <summary>
        ///   Every title of the group: its English name.
        /// </summary>
        /// <returns>The titles.</returns>
        public IReadOnlyList<ITitle> GetAllTitles() => [GetPreferredTitle()];
    }

    /// <summary>
    ///   Where one of TMDB's alternate orderings places an episode.
    /// </summary>
    public sealed class AlternateOrderingEpisode
    {
        private readonly ISeason _season;

        /// <summary>
        ///   A place of an episode in a group of an ordering.
        /// </summary>
        /// <param name="ordering">The ordering.</param>
        /// <param name="season">The group, as a season of the ordering.</param>
        /// <param name="episodeID">The episode.</param>
        /// <param name="episodeNumber">The episode's number in the group.</param>
        /// <param name="airing">Where a placed special airs, on its place in the special group only.</param>
        internal AlternateOrderingEpisode(AlternateOrdering ordering, ISeason season, MetadataGuid episodeID, int episodeNumber, OrderingAiring? airing)
        {
            TmdbAlternateOrdering = ordering;
            _season = season;
            EpisodeID = episodeID;
            PlacedEpisodeNumber = episodeNumber;
            AirsBeforeSeasonNumber = airing?.AirsBeforeSeasonNumber;
            AirsBeforeEpisodeNumber = airing?.AirsBeforeEpisodeNumber;
            AirsAfterSeasonNumber = airing?.AirsAfterSeasonNumber;
            AirsBeforeEpisodeID = airing?.AirsBeforeEpisodeID;
        }

        /// <summary>
        ///   The episode.
        /// </summary>
        internal MetadataGuid EpisodeID { get; }

        /// <summary>
        ///   The regular episode right after a placed special, in any group.
        /// </summary>
        internal MetadataGuid? AirsBeforeEpisodeID { get; }

        /// <summary>
        ///   TMDB's ID for the episode group collection.
        /// </summary>
        public string TmdbEpisodeGroupCollectionID => TmdbAlternateOrdering.TmdbEpisodeGroupCollectionID;

        /// <summary>
        ///   TMDB's ID for the episode group the episode is placed in.
        /// </summary>
        public string TmdbEpisodeGroupID => _season.ID.ID;

        /// <summary>
        ///   TMDB's ID for the episode.
        /// </summary>
        public int TmdbEpisodeID => Number(EpisodeID.ID);

        /// <summary>
        ///   TMDB's ID for the show.
        /// </summary>
        public int TmdbShowID => TmdbAlternateOrdering.TmdbShowID;

        /// <summary>
        ///   The number of the group the episode is placed in.
        /// </summary>
        public int SeasonNumber => _season.SeasonNumber;

        /// <summary>
        ///   The episode's number in the group.
        /// </summary>
        public int PlacedEpisodeNumber { get; }

        /// <summary>
        ///   The season of the regular episode a placed special airs before,
        ///   on its place in the special group.
        /// </summary>
        public int? AirsBeforeSeasonNumber { get; }

        /// <summary>
        ///   The number of the regular episode a placed special airs before.
        /// </summary>
        public int? AirsBeforeEpisodeNumber { get; private set; }

        /// <summary>
        ///   The season a placed special airs after, when no episode of it
        ///   follows the special.
        /// </summary>
        public int? AirsAfterSeasonNumber { get; }

        /// <summary>
        ///   Whether the ordering is the one chosen for the show.
        /// </summary>
        public bool IsPreferred => TmdbAlternateOrdering.Ordering.IsPreferred;

        /// <summary>
        ///   The episode.
        /// </summary>
        public Metadata_Episode? TmdbEpisode => GetEpisode(TmdbEpisodeID);

        /// <summary>
        ///   The ordering.
        /// </summary>
        public AlternateOrdering TmdbAlternateOrdering { get; }

        /// <summary>
        ///   The group the episode is placed in.
        /// </summary>
        public AlternateOrderingSeason? TmdbAlternateOrderingSeason
            => TmdbAlternateOrdering.Seasons.FirstOrDefault(season => season.TmdbEpisodeGroupID == TmdbEpisodeGroupID);

        /// <summary>
        ///   The place, airing before another number of its episode.
        /// </summary>
        /// <param name="episodeNumber">The number of the regular episode the special airs before.</param>
        /// <returns>The place.</returns>
        internal AlternateOrderingEpisode WithAirsBeforeEpisodeNumber(int episodeNumber)
        {
            var place = (AlternateOrderingEpisode)MemberwiseClone();
            place.AirsBeforeEpisodeNumber = episodeNumber;
            return place;
        }
    }

    #endregion
}
