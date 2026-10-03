using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Turns TMDb's shows and movies, searched, fetched or stored, into the
///   results a search offers.
/// </summary>
/// <remarks>
///   TMDb names the genres of a search hit by ID only, so their names are
///   looked up through the genre lookup given, which reads the tag store. A
///   genre joining two with <c>&amp;</c> gives each part.
/// </remarks>
public static class TmdbSearchResults
{
    #region Shows

    /// <summary>
    ///   A show a search turned up.
    /// </summary>
    /// <param name="show">The show.</param>
    /// <param name="imageServerUrl">TMDb's image server.</param>
    /// <param name="genreName">Looks a genre's names up by its ID.</param>
    /// <returns>The result.</returns>
    public static MetadataSeriesSearchResult FromSearch(SearchTv show, string imageServerUrl, Func<int, IReadOnlyList<string>> genreName)
        => new()
        {
            ID = TmdbIds.Series(show.Id),
            Title = TmdbTexts.Clean(show.Name) ?? TmdbTexts.Clean(show.OriginalName) ?? string.Empty,
            OriginalTitle = TmdbTexts.Clean(show.OriginalName),
            OriginalLanguageCode = TmdbTexts.Clean(show.OriginalLanguage),
            Overview = TmdbTexts.Clean(show.Overview),
            UserRating = (decimal)show.VoteAverage,
            UserVotes = show.VoteCount,
            PosterUrl = TmdbImages.Url(imageServerUrl, show.PosterPath),
            BackdropUrl = TmdbImages.Url(imageServerUrl, show.BackdropPath),
            Genres = GenreNames(show.GenreIds, genreName),
            FirstAiredAt = PartialDateOnly.FromDateTime(show.FirstAirDate),
        };

    /// <summary>
    ///   A show as TMDb answered it, with its translated names when it was
    ///   fetched with its translations, and its regular seasons.
    /// </summary>
    /// <param name="show">The show.</param>
    /// <param name="imageServerUrl">TMDb's image server.</param>
    /// <returns>The result.</returns>
    public static MetadataSeriesSearchResult FromShow(TvShow show, string imageServerUrl)
        => new()
        {
            ID = TmdbIds.Series(show.Id),
            Title = TmdbTexts.English(show.Translations, data => data.Name, show.Name, show.OriginalName, show.OriginalLanguage) ?? TmdbTexts.Clean(show.OriginalName) ?? string.Empty,
            OriginalTitle = TmdbTexts.Clean(show.OriginalName),
            AlternateTitles = [.. (show.Translations?.Translations ?? []).Select(translation => TmdbTexts.Clean(translation.Data?.Name)).OfType<string>().Distinct()],
            Seasons =
            [
                .. (show.Seasons ?? []).Where(season => season.SeasonNumber > 0).Select(season => new MetadataSearchResultSeason
                {
                    SeasonNumber = season.SeasonNumber,
                    EpisodeCount = season.EpisodeCount,
                    FirstAiredAt = PartialDateOnly.FromDateTime(season.AirDate),
                }),
            ],
            OriginalLanguageCode = TmdbTexts.Clean(show.OriginalLanguage),
            Overview = TmdbTexts.Clean(show.Overview),
            IsRestricted = show.Adult,
            UserRating = (decimal)show.VoteAverage,
            UserVotes = show.VoteCount,
            PosterUrl = TmdbImages.Url(imageServerUrl, show.PosterPath),
            BackdropUrl = TmdbImages.Url(imageServerUrl, show.BackdropPath),
            Genres = [.. (show.Genres ?? []).SelectMany(genre => TmdbEntityMapper.GenreNames(genre.Name)).Distinct(StringComparer.Ordinal)],
            FirstAiredAt = PartialDateOnly.FromDateTime(show.FirstAirDate),
            EpisodeCount = show.NumberOfEpisodes > 0 ? show.NumberOfEpisodes : null,
        };

    /// <summary>
    ///   A stored show, with the episodes of its regular seasons, so a match
    ///   can line them up without asking TMDb.
    /// </summary>
    /// <param name="series">The stored show.</param>
    /// <param name="imageServerUrl">TMDb's image server.</param>
    /// <returns>The result.</returns>
    public static MetadataSeriesSearchResult FromStored(ISeries series, string imageServerUrl)
    {
        var episodes = series.Episodes
            .Where(episode => episode.SeasonNumber is > 0)
            .GroupBy(episode => episode.SeasonNumber!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MetadataSearchResultEpisode>)
                [
                    .. group.OrderBy(episode => episode.EpisodeNumber).Select(episode => new MetadataSearchResultEpisode
                    {
                        EpisodeNumber = episode.EpisodeNumber,
                        AiredAt = episode.AirDate,
                        Title = episode.DefaultTitle.Value,
                    }),
                ]
            );
        var episodeCount = series.Episodes.Count(episode => episode.SeasonNumber is > 0);
        return new()
        {
            ID = series.ID,
            Title = series.DefaultTitle.Value,
            OriginalTitle = OriginalTitle(series, series.OriginalLanguageCode),
            OriginalLanguageCode = series.OriginalLanguageCode,
            Overview = series.DefaultOverview?.Value,
            IsRestricted = series.Restricted,
            UserRating = (decimal)series.Rating,
            UserVotes = series.RatingVotes,
            PosterUrl = StoredImageUrl(series.PrimaryImage, imageServerUrl),
            BackdropUrl = StoredImageUrl(series.BackdropImage, imageServerUrl),
            Genres = GenresOf(series.Tags),
            FirstAiredAt = series.AirDate,
            EpisodeCount = episodeCount > 0 ? episodeCount : null,
            Seasons =
            [
                .. series.Seasons
                    .Where(season => season.SeasonNumber > 0)
                    .OrderBy(season => season.SeasonNumber)
                    .Select(season => episodes.TryGetValue(season.SeasonNumber, out var listed)
                        ? new MetadataSearchResultSeason
                        {
                            SeasonNumber = season.SeasonNumber,
                            EpisodeCount = listed.Count,
                            FirstAiredAt = PartialDateOnly.FromDateOnly(listed.Select(episode => episode.AiredAt).Min()),
                            FirstEpisodeAiredAt = listed[0].AiredAt,
                            Episodes = listed,
                        }
                        : new MetadataSearchResultSeason { SeasonNumber = season.SeasonNumber }),
            ],
        };
    }

    #endregion

    #region Movies

    /// <summary>
    ///   A movie a search turned up.
    /// </summary>
    /// <param name="movie">The movie.</param>
    /// <param name="imageServerUrl">TMDb's image server.</param>
    /// <param name="genreName">Looks a genre's names up by its ID.</param>
    /// <returns>The result.</returns>
    public static MetadataMovieSearchResult FromSearch(SearchMovie movie, string imageServerUrl, Func<int, IReadOnlyList<string>> genreName)
        => new()
        {
            ID = TmdbIds.Movie(movie.Id),
            Title = TmdbTexts.Clean(movie.Title) ?? TmdbTexts.Clean(movie.OriginalTitle) ?? string.Empty,
            OriginalTitle = TmdbTexts.Clean(movie.OriginalTitle),
            OriginalLanguageCode = TmdbTexts.Clean(movie.OriginalLanguage),
            Overview = TmdbTexts.Clean(movie.Overview),
            IsRestricted = movie.Adult,
            UserRating = (decimal)movie.VoteAverage,
            UserVotes = movie.VoteCount,
            PosterUrl = TmdbImages.Url(imageServerUrl, movie.PosterPath),
            BackdropUrl = TmdbImages.Url(imageServerUrl, movie.BackdropPath),
            Genres = GenreNames(movie.GenreIds, genreName),
            ReleasedAt = PartialDateOnly.FromDateTime(movie.ReleaseDate),
            IsStandaloneVideo = movie.Video,
        };

    /// <summary>
    ///   A movie as TMDb answered it, with its translated titles and its
    ///   release dates in every country when it was fetched with them.
    /// </summary>
    /// <param name="movie">The movie.</param>
    /// <param name="imageServerUrl">TMDb's image server.</param>
    /// <returns>The result.</returns>
    public static MetadataMovieSearchResult FromMovie(Movie movie, string imageServerUrl)
        => new()
        {
            ID = TmdbIds.Movie(movie.Id),
            Title = TmdbTexts.English(movie.Translations, data => data.Name, movie.Title, movie.OriginalTitle, movie.OriginalLanguage) ?? TmdbTexts.Clean(movie.OriginalTitle) ?? string.Empty,
            OriginalTitle = TmdbTexts.Clean(movie.OriginalTitle),
            AlternateTitles = [.. (movie.Translations?.Translations ?? []).Select(translation => TmdbTexts.Clean(translation.Data?.Name)).OfType<string>().Distinct()],
            OtherReleaseDates =
            [
                .. (movie.ReleaseDates?.Results ?? [])
                    .SelectMany(country => country.ReleaseDates ?? [])
                    .Select(release => DateOnly.FromDateTime(release.ReleaseDate))
                    .Distinct(),
            ],
            OriginalLanguageCode = TmdbTexts.Clean(movie.OriginalLanguage),
            Overview = TmdbTexts.Clean(movie.Overview),
            IsRestricted = movie.Adult,
            UserRating = (decimal)movie.VoteAverage,
            UserVotes = movie.VoteCount,
            PosterUrl = TmdbImages.Url(imageServerUrl, movie.PosterPath),
            BackdropUrl = TmdbImages.Url(imageServerUrl, movie.BackdropPath),
            Genres = [.. (movie.Genres ?? []).SelectMany(genre => TmdbEntityMapper.GenreNames(genre.Name)).Distinct(StringComparer.Ordinal)],
            ReleasedAt = PartialDateOnly.FromDateTime(movie.ReleaseDate),
            IsStandaloneVideo = movie.Video,
        };

    /// <summary>
    ///   A stored movie.
    /// </summary>
    /// <param name="movie">The stored movie.</param>
    /// <param name="imageServerUrl">TMDb's image server.</param>
    /// <returns>The result.</returns>
    public static MetadataMovieSearchResult FromStored(IMovie movie, string imageServerUrl)
        => new()
        {
            ID = movie.ID,
            Title = movie.DefaultTitle.Value,
            OriginalTitle = OriginalTitle(movie, movie.OriginalLanguageCode),
            OriginalLanguageCode = movie.OriginalLanguageCode,
            Overview = movie.DefaultOverview?.Value,
            IsRestricted = movie.Restricted,
            UserRating = (decimal)movie.Rating,
            UserVotes = movie.RatingVotes,
            PosterUrl = StoredImageUrl(movie.PrimaryImage, imageServerUrl),
            BackdropUrl = StoredImageUrl(movie.BackdropImage, imageServerUrl),
            Genres = GenresOf(movie.Tags),
            ReleasedAt = movie.ReleaseDate is { } released ? PartialDateOnly.FromDateTime(released) : null,
            IsStandaloneVideo = movie.Video,
        };

    #endregion

    #region Helpers

    /// <summary>
    ///   The names of the genres a search hit names by ID, in order, leaving
    ///   out the ones not known.
    /// </summary>
    /// <param name="genreIDs">The genre IDs.</param>
    /// <param name="genreName">Looks a genre's names up by its ID.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> GenreNames(IEnumerable<int>? genreIDs, Func<int, IReadOnlyList<string>> genreName)
        => [.. (genreIDs ?? []).SelectMany(genreName).Distinct(StringComparer.Ordinal)];

    private static IReadOnlyList<string> GenresOf(IEnumerable<ITag> tags)
        => [.. tags.Where(tag => tag.Kind is TagKind.Genre && tag.ID.Source == MetadataSource.TMDB).Select(tag => tag.Name)];

    private static string? OriginalTitle(Shoko.Abstractions.Metadata.Containers.IWithTitles entry, string? originalLanguageCode)
        => string.IsNullOrEmpty(originalLanguageCode)
            ? null
            : entry.Titles.FirstOrDefault(title => title.Source == MetadataSource.TMDB && title.Type is TitleType.Official &&
                string.Equals(title.LanguageCode, originalLanguageCode, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? StoredImageUrl(Shoko.Abstractions.Metadata.Image.IImage? image, string imageServerUrl)
        => image is { } stored && stored.Source == MetadataSource.TMDB ? string.Format(TmdbImages.Template(imageServerUrl), stored.ResourceID) : null;

    #endregion
}
