using System.Collections.Generic;
using System.Linq;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// Adds the optional details of the AniDB anime list to its models, looked
/// up for one page of anime at a time.
/// </summary>
public static class AnidbAnimeDetails
{
    /// <summary>
    /// Looks up what a page needs in one go: the studios, when asked for.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="entries">The page's anime.</param>
    /// <param name="include">The details asked for.</param>
    /// <returns>The studios by anime, or <c>null</c> when not asked for.</returns>
    public static IReadOnlyDictionary<int, IReadOnlyList<AniDB_Creator>>? LoadStudios(
        AnidbAnimeCatalog catalog,
        IEnumerable<(AniDB_Anime Anime, AnimeSeries? Series)> entries,
        IReadOnlySet<AnidbAnime.IncludeDetails> include
    )
        => include.Contains(AnidbAnime.IncludeDetails.Studios)
            ? catalog.GetStudios([.. entries.Select(entry => entry.Anime.AnimeID)])
            : null;

    /// <summary>
    /// Sets the details asked for on one model, and leaves the rest unset.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="anime">The anime.</param>
    /// <param name="series">Its Shoko series, if any.</param>
    /// <param name="include">The details asked for.</param>
    /// <param name="tagLimit">The most tags to add.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="studios">The page's studios, from <see cref="LoadStudios"/>.</param>
    public static void Apply(
        AnidbAnime model,
        AniDB_Anime anime,
        AnimeSeries? series,
        IReadOnlySet<AnidbAnime.IncludeDetails> include,
        int tagLimit,
        AnidbAnimeCatalog catalog,
        IReadOnlyDictionary<int, IReadOnlyList<AniDB_Creator>>? studios
    )
    {
        if (include.Contains(AnidbAnime.IncludeDetails.Overview))
            model.Overview = catalog.GetOverview(anime, series);

        if (include.Contains(AnidbAnime.IncludeDetails.Studios))
            model.Studios = studios is not null && studios.TryGetValue(anime.AnimeID, out var list)
                ? [.. list.Select(creator => new AnidbAnime.AnimeStudio { ID = creator.CreatorID, Name = creator.Name })]
                : [];

        if (include.Contains(AnidbAnime.IncludeDetails.SourceMaterial))
            model.SourceMaterial = anime.SourceMaterial;

        if (include.Contains(AnidbAnime.IncludeDetails.Tags))
            model.Tags = [.. catalog.GetGenreTags(anime.AnimeID, tagLimit).Select(tag => new AnidbAnime.AnimeTag { ID = tag.TagID, Name = tag.TagName })];

        if (include.Contains(AnidbAnime.IncludeDetails.Files))
            model.Files = new() { VideoCount = series is null ? 0 : catalog.GetVideoCount(anime.AnimeID) };

        if (include.Contains(AnidbAnime.IncludeDetails.StartSeason))
            model.StartSeason = catalog.GetStartSeason(anime) is { } season ? new SeasonWithYear(season.Year, season.Season) : null;

        if (include.Contains(AnidbAnime.IncludeDetails.EpisodeDuration))
            model.EpisodeDuration = catalog.GetEpisodeDuration(anime.AnimeID);
    }
}
