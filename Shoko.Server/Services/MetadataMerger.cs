using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
/// Merges an AniDB anime's genres and studios with those of the series and
/// movies of other sources it is linked to, for
/// <see cref="IMetadataService.GetMergedGenres"/> and
/// <see cref="IMetadataService.GetMergedStudios"/>.
/// </summary>
/// <param name="catalog">The cached anime catalog, for AniDB's genre tags and studios.</param>
/// <param name="metadataService">The metadata service, for the linked entries of other sources.</param>
/// <param name="configurationProvider">The settings, for the saved options.</param>
public class MetadataMerger(
    AnidbAnimeCatalog catalog,
    IMetadataService metadataService,
    ConfigurationProvider<MetadataServiceSettings> configurationProvider
)
{
    /// <summary>
    /// The saved options. Setting them saves them.
    /// </summary>
    public MetadataMergeOptions Options
    {
        get
        {
            var settings = configurationProvider.Load().Merge;
            return new()
            {
                SourceOrder = [.. settings.SourceOrder],
                GenreLimit = settings.GenreLimit,
                ExcludedGenres = [.. settings.ExcludedGenres],
            };
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfNegative(value.GenreLimit);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GenreLimit, MetadataMergeSettings.MaximumGenreLimit);
            var settings = configurationProvider.Load(copy: true);
            settings.Merge.SourceOrder = [.. value.SourceOrder];
            settings.Merge.GenreLimit = value.GenreLimit;
            settings.Merge.ExcludedGenres = [.. value.ExcludedGenres];
            configurationProvider.Save(settings);
        }
    }

    /// <inheritdoc cref="IMetadataService.GetMergedGenres"/>
    public IReadOnlyList<ITag> GetGenres(int anidbAnimeID, MetadataMergeOptions? options = null)
    {
        options ??= Options;
        var sourceOrder = GetSourceOrder(options.SourceOrder);
        return MergeGenres(
            [.. catalog.GetGenreTags(anidbAnimeID, int.MaxValue)],
            GetLinkedEntries(anidbAnimeID, sourceOrder),
            sourceOrder,
            options.GenreLimit,
            options.ExcludedGenres
        );
    }

    /// <inheritdoc cref="IMetadataService.GetMergedStudios"/>
    public IReadOnlyList<IStudio> GetStudios(int anidbAnimeID, MetadataMergeOptions? options = null)
    {
        options ??= Options;
        var sourceOrder = GetSourceOrder(options.SourceOrder);
        var anidbStudios = catalog.GetStudios([anidbAnimeID]).TryGetValue(anidbAnimeID, out var list)
            ? [.. list.Select(creator => (IStudio)new AniDB_Studio(creator))]
            : new List<IStudio>();
        return MergeStudios(anidbStudios, GetLinkedEntries(anidbAnimeID, sourceOrder), sourceOrder);
    }

    /// <summary>
    /// The sources to use, highest ranked first: the listed ones, with AniDB
    /// at its place when listed, else first.
    /// </summary>
    /// <param name="listed">The options' source order.</param>
    /// <returns>The sources, AniDB included, each once.</returns>
    internal static IReadOnlyList<MetadataSource> GetSourceOrder(IEnumerable<MetadataSource> listed)
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
    internal static List<IStudio> MergeStudios(
        IReadOnlyList<IStudio> anidbStudios,
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
                .Concat(extra)
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
    /// restricted genres and the excluded genres are left out.
    /// </remarks>
    /// <param name="anidbGenres">AniDB's genre tags, heaviest first.</param>
    /// <param name="linked">The linked entries.</param>
    /// <param name="sourceOrder">The sources, AniDB included, highest ranked first.</param>
    /// <param name="limit">The most genres to return.</param>
    /// <param name="excludedGenres">The genres to leave out, by name.</param>
    /// <returns>The genres.</returns>
    internal static List<ITag> MergeGenres(
        IReadOnlyList<ITag> anidbGenres,
        IReadOnlyList<LinkedEntry> linked,
        IReadOnlyList<MetadataSource> sourceOrder,
        int limit,
        IReadOnlyList<string> excludedGenres
    )
    {
        var excluded = excludedGenres.Select(GetNameKey).ToHashSet(StringComparer.Ordinal);
        var candidates = anidbGenres
            .Concat(
                linked
                    .SelectMany(entry => entry.Tags)
                    .Where(tag => tag is { Kind: TagKind.Genre, IsSpoiler: false, IsRestricted: false })
            )
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
            .Select(tag => (Tag: tag, Key: GetNameKey(tag.Name)))
            .Where(candidate => !excluded.Contains(candidate.Key))
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
}
