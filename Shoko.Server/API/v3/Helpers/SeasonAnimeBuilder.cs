using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Models.Airing;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// Maps the season view's anime to their APIv3 models: the details from the
/// catalog, and the studios and genres from AniDB and the linked sources the
/// settings rank, AniDB at its place in that order or else first. The
/// airings, the sorting and the grouping come from the airing calendar service.
/// </summary>
/// <param name="catalog">The cached anime catalog.</param>
/// <param name="metadataService">The metadata service, for the linked entries of other sources.</param>
/// <param name="configurationProvider">The airing schedule settings, for the ranked sources.</param>
public class SeasonAnimeBuilder(
    AnidbAnimeCatalog catalog,
    IMetadataService metadataService,
    ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider
)
{
    #region Constants

    /// <summary>
    /// The name keys of the genres that say nothing on an anime-only page.
    /// </summary>
    private static readonly HashSet<string> _uninformativeGenres = new(StringComparer.Ordinal) { "anime", "animation" };

    #endregion

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

        var sourceOrder = GetSourceOrder(configurationProvider.Load().SeasonDetailSourceOrder);
        var studios = catalog.GetStudios([.. entries.Select(entry => entry.Anime.AnidbID)]);
        return [.. entries.Select(entry => Build(entry, studios, sourceOrder))];
    }

    /// <summary>
    /// Builds the model for one anime.
    /// </summary>
    /// <param name="entry">The anime, read by the airing calendar service from the catalog.</param>
    /// <param name="studios">The studios of every anime being built.</param>
    /// <param name="sourceOrder">The sources to use, AniDB included, highest ranked first.</param>
    /// <returns>The model.</returns>
    private SeasonAnime Build(
        SeasonAnimeEntry entry,
        IReadOnlyDictionary<int, IReadOnlyList<AniDB_Creator>> studios,
        IReadOnlyList<MetadataSource> sourceOrder
    )
    {
        // The core service reads its entries from the catalog, so they are always the stored models.
        var anime = (AniDB_Anime)entry.Anime;
        var series = (AnimeSeries?)entry.Series;
        var linked = GetLinkedEntries(anime.AnimeID, sourceOrder);
        return new()
        {
            ID = anime.AnimeID,
            ShokoID = series?.AnimeSeriesID,
            Type = anime.AnimeType,
            Title = entry.Title,
            Poster = catalog.GetPoster(anime, series) is { } poster ? new Image(poster) : null,
            Overview = catalog.GetOverview(anime, series),
            AirDate = anime.AirDate,
            EndDate = anime.EndDate,
            EpisodeCount = anime.EpisodeCountNormal,
            Restricted = anime.IsRestricted,
            Studios = MergeStudios(
                studios.TryGetValue(anime.AnimeID, out var list)
                    ? [.. list.Select(creator => new SeasonAnime.Studio { Source = MetadataSource.AniDB, ID = creator.CreatorID.ToString(), Name = creator.Name })]
                    : [],
                linked,
                sourceOrder
            ),
            SourceMaterial = anime.SourceMaterial,
            Tags = MergeGenres(
                [.. catalog.GetGenreTags(anime.AnimeID, int.MaxValue).Select(tag => new SeasonAnime.Tag { Source = MetadataSource.AniDB, ID = tag.TagID.ToString(), Name = tag.TagName })],
                linked,
                sourceOrder,
                SeasonAnime.TagLimit
            ),
            VideoCount = series is null ? 0 : catalog.GetVideoCount(anime.AnimeID),
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

    #region Studios and Genres

    /// <summary>
    /// The sources to use, highest ranked first: the listed ones, with AniDB
    /// at its place when listed, else first.
    /// </summary>
    /// <param name="listed">The season detail source order from the settings.</param>
    /// <returns>The sources, AniDB included, each once.</returns>
    private static IReadOnlyList<MetadataSource> GetSourceOrder(IEnumerable<MetadataSource> listed)
    {
        var sources = listed.Distinct().ToList();
        return sources.Contains(MetadataSource.AniDB) ? sources : [MetadataSource.AniDB, .. sources];
    }

    /// <summary>
    /// The series and movies of the listed sources other than AniDB an anime
    /// is linked to.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="sourceOrder">The sources to use, highest ranked first.</param>
    /// <returns>The linked entries by source rank, each source's series before its movies.</returns>
    private IReadOnlyList<LinkedEntry> GetLinkedEntries(int animeID, IReadOnlyList<MetadataSource> sourceOrder)
        =>
        [
            .. sourceOrder
                .Where(source => source != MetadataSource.AniDB)
                .SelectMany(source => GetLinkedEntries(animeID, source)),
        ];

    /// <summary>
    /// The series and movies of one source an anime is linked to.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="source">The source.</param>
    /// <returns>The linked entries, series first, in link order.</returns>
    private IEnumerable<LinkedEntry> GetLinkedEntries(int animeID, MetadataSource source)
        =>
        [
            .. metadataService.GetSeriesCrossReferences(animeID, source)
                .Select(xref => xref.Provider)
                .OfType<ISeries>()
                .Select(series => new LinkedEntry(source, series.Studios, series.Tags)),
            .. metadataService.GetMovieCrossReferencesForSeries(animeID, source)
                .Select(xref => xref.Provider)
                .OfType<IMovie>()
                .DistinctBy(movie => movie.ID)
                .Select(movie => new LinkedEntry(source, movie.Studios, movie.Tags)),
        ];

    /// <summary>
    /// Merges AniDB's animation studios with those of the linked sources.
    /// </summary>
    /// <remarks>
    /// Each source's animation studios come in its order, the sources by
    /// rank, and a name a higher ranked source gave is not repeated, compared
    /// by <see cref="GetNameKey"/>. When no source names an animation studio,
    /// the untyped studios of the highest ranked source with any fill in.
    /// Production studios are never used.
    /// </remarks>
    /// <param name="anidbStudios">AniDB's animation studios.</param>
    /// <param name="linked">The linked entries.</param>
    /// <param name="sourceOrder">The sources, AniDB included, highest ranked first.</param>
    /// <returns>The studios.</returns>
    internal static List<SeasonAnime.Studio> MergeStudios(
        IReadOnlyList<SeasonAnime.Studio> anidbStudios,
        IReadOnlyList<LinkedEntry> linked,
        IReadOnlyList<MetadataSource> sourceOrder
    )
    {
        var ranked = linked
            .OrderBy(entry => GetRank(sourceOrder, entry.Source))
            .ToList();
        var extra = ranked
            .SelectMany(entry => entry.Studios)
            .Where(studio => studio.StudioType is StudioType.Animation)
            .ToList();
        if (extra.Count is 0 && anidbStudios.Count is 0 && ranked.FirstOrDefault(entry => entry.Studios.Any(IsUntyped)) is { } top)
        {
            extra =
            [
                .. ranked
                    .Where(entry => entry.Source == top.Source)
                    .SelectMany(entry => entry.Studios)
                    .Where(IsUntyped),
            ];
        }

        return
        [
            .. anidbStudios
                .Concat(extra.Select(studio => new SeasonAnime.Studio { Source = studio.Source, ID = studio.ID.ID, Name = studio.Name }))
                .OrderBy(studio => GetRank(sourceOrder, studio.Source))
                .Where(studio => !string.IsNullOrWhiteSpace(studio.Name))
                .DistinctBy(studio => GetNameKey(studio.Name), StringComparer.Ordinal),
        ];

        static bool IsUntyped(IStudio studio)
            => studio.StudioType is StudioType.None && !string.IsNullOrWhiteSpace(studio.Name);
    }

    /// <summary>
    /// Merges AniDB's genre tags with the genres of the linked sources, and
    /// keeps those most sources agree on.
    /// </summary>
    /// <remarks>
    /// Names are matched by <see cref="GetNameKey"/>, keeping the highest
    /// ranked source's spelling. A genre counts once per source that lists it; ties go by
    /// source rank, then by the order the source lists them in. Spoilers,
    /// adult genres and genres that only say "anime" are left out.
    /// </remarks>
    /// <param name="anidbGenres">AniDB's genre tags, heaviest first.</param>
    /// <param name="linked">The linked entries.</param>
    /// <param name="sourceOrder">The sources, AniDB included, highest ranked first.</param>
    /// <param name="limit">The most genres to return.</param>
    /// <returns>The genres.</returns>
    internal static List<SeasonAnime.Tag> MergeGenres(
        IReadOnlyList<SeasonAnime.Tag> anidbGenres,
        IReadOnlyList<LinkedEntry> linked,
        IReadOnlyList<MetadataSource> sourceOrder,
        int limit
    )
    {
        var candidates = anidbGenres
            .Concat(
                linked
                    .SelectMany(entry => entry.Tags)
                    .Where(tag => tag is { Kind: TagKind.Genre, IsSpoiler: false, IsRestricted: false })
                    .Select(tag => new SeasonAnime.Tag { Source = tag.Source, ID = tag.ID.ID, Name = tag.Name })
            )
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
            .Select(tag => (Tag: tag, Key: GetNameKey(tag.Name)))
            .Where(candidate => !_uninformativeGenres.Contains(candidate.Key))
            .OrderBy(candidate => GetRank(sourceOrder, candidate.Tag.Source))
            .Select((candidate, index) => (candidate.Tag, candidate.Key, Index: index))
            .ToList();
        return
        [
            .. candidates
                .GroupBy(candidate => candidate.Key, StringComparer.Ordinal)
                .Select(group => (
                    First: group.First(),
                    Sources: group.Select(candidate => candidate.Tag.Source).Distinct().Count()
                ))
                .OrderByDescending(group => group.Sources)
                .ThenBy(group => group.First.Index)
                .Take(limit)
                .Select(group => group.First.Tag),
        ];
    }

    /// <summary>
    /// The key a studio or genre name is compared by across sources.
    /// </summary>
    /// <remarks>
    /// The name in NFKD without its Latin diacritics, back in NFKC, keeping
    /// only its letters, digits and '+' (which sets e.g. <c>Disney+</c>
    /// apart), lower-cased. A name with none of them falls back to itself,
    /// trimmed and lower-cased.
    /// </remarks>
    /// <param name="name">The name.</param>
    /// <returns>The key.</returns>
    internal static string GetNameKey(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            // Only the combining diacritical marks block, so kana keep their voicing marks.
            if (character is >= '̀' and <= 'ͯ')
                continue;
            builder.Append(character);
        }

        var key = new string(
            builder.ToString()
                .Normalize(NormalizationForm.FormKC)
                .Where(character => char.IsLetterOrDigit(character) || character is '+')
                .ToArray()
        ).ToLowerInvariant();
        return key.Length > 0 ? key : name.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// A source's place in the order, with sources not in it last.
    /// </summary>
    /// <param name="sourceOrder">The sources, highest ranked first.</param>
    /// <param name="source">The source.</param>
    /// <returns>The rank, lowest first.</returns>
    private static int GetRank(IReadOnlyList<MetadataSource> sourceOrder, MetadataSource source)
        => sourceOrder.IndexOf(source) is var rank and >= 0 ? rank : int.MaxValue;

    /// <summary>
    /// The studios and tags of a linked series or movie.
    /// </summary>
    /// <param name="Source">The source it is from.</param>
    /// <param name="Studios">Its studios.</param>
    /// <param name="Tags">Its tags.</param>
    internal sealed record LinkedEntry(MetadataSource Source, IReadOnlyList<IStudio> Studios, IReadOnlyList<ITag> Tags);

    #endregion
}
