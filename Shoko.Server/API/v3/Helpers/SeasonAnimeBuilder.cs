using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.API.v3.Models.Airing;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Services;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// Maps the season view's anime to their APIv3 models: the details from the
/// catalog. The genres and studios, merged across sources by the metadata
/// service, and the airings, the sorting and the grouping come from the
/// airing calendar service.
/// </summary>
/// <param name="catalog">The cached anime catalog.</param>
public class SeasonAnimeBuilder(AnidbAnimeCatalog catalog)
{
    #region Building

    /// <summary>
    /// Builds the models for the anime of a season, in their order.
    /// </summary>
    /// <param name="entries">The anime, as the airing calendar service read them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <c>null</c>.</exception>
    /// <returns>The models.</returns>
    public List<SeasonAnime> Build(IReadOnlyList<SeasonAnimeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return [.. entries.Select(Build)];
    }

    /// <summary>
    /// Builds the model for one anime.
    /// </summary>
    /// <param name="entry">The anime, read by the airing calendar service from the catalog.</param>
    /// <returns>The model.</returns>
    private SeasonAnime Build(SeasonAnimeEntry entry)
    {
        var anime = entry.Anime;
        var series = entry.Series;
        return new()
        {
            ID = anime.AnidbID,
            ShokoID = series?.LocalID,
            Type = anime.Type,
            Title = entry.Title,
            Poster = catalog.GetPoster(anime, series) is { } poster ? new Image(poster) : null,
            Overview = catalog.GetOverview(anime, series),
            AirDate = anime.AirDate,
            EndDate = anime.EndDate,
            EpisodeCount = anime.EpisodeCounts.Episodes,
            Restricted = anime.Restricted,
            Studios = [.. entry.Studios.Select(studio => new SeasonAnime.Studio { Source = studio.Source, ID = studio.ID.ID, Name = studio.Name })],
            SourceMaterial = anime.SourceMaterial,
            Tags = [.. entry.Genres.Select(genre => new SeasonAnime.Tag { Source = genre.Source, ID = genre.ID.ID, Name = genre.Name })],
            VideoCount = series is null ? 0 : catalog.GetVideoCount(anime.AnidbID),
            StartSeason = entry.StartSeason is { } season ? new SeasonWithYear(season.Year, season.Season) : null,
            IsStartSeasonOverridden = entry.IsStartSeasonOverridden,
            EpisodeDuration = entry.EpisodeDuration,
            AiringStatus = entry.Status switch
            {
                SeasonAnimeAiringStatus.Upcoming => SeasonAnime.NextAiringStatus.Upcoming,
                SeasonAnimeAiringStatus.Finished => SeasonAnime.NextAiringStatus.Finished,
                _ => SeasonAnime.NextAiringStatus.Unknown,
            },
            NextAiring = entry.NextAiring is { } next ? new EpisodeAiring(next) : null,
            OtherAirings = [.. entry.OtherAirings.Select(airing => new EpisodeAiring(airing))],
        };
    }

    #endregion
}
