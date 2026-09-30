using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories.Cached.Metadata.Text;

namespace Shoko.Server.Services;

/// <summary>
///   Matches a search against the main and official titles AniDB gave each
///   anime, normalized once and kept in step with the stored titles.
/// </summary>
public sealed class AnidbTitleSearch
{
    #region Fields

    private readonly TextCache _cache;

    private readonly Lock _buildLock = new();

    private ConcurrentDictionary<int, string[]>? _titles;

    #endregion

    #region Constructor

    /// <summary>
    ///   Creates the search over the stored titles.
    /// </summary>
    /// <param name="cache">The text cache holding every stored title.</param>
    public AnidbTitleSearch(TextCache cache)
    {
        _cache = cache;
        _cache.Changed += OnTextsChanged;
    }

    #endregion

    #region Searching

    /// <summary>
    ///   Normalizes a title or a search the way the titles are matched.
    /// </summary>
    /// <param name="value">The title or search.</param>
    /// <returns>The value in compatibility form, in lower case.</returns>
    public static string NormalizeForSearch(string value)
        => value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();

    /// <summary>
    ///   Whether one of an anime's main or official AniDB titles holds a
    ///   search.
    /// </summary>
    /// <param name="animeID">The AniDB anime.</param>
    /// <param name="normalizedQuery">The search, normalized with <see cref="NormalizeForSearch"/>.</param>
    /// <returns><c>true</c> when a title holds it.</returns>
    public bool AnimeMatchesSearch(int animeID, string normalizedQuery)
        => Titles().TryGetValue(animeID, out var titles) && titles.Any(title => title.Contains(normalizedQuery, StringComparison.Ordinal));

    #endregion

    #region Indexing

    /// <summary>
    ///   The normalized titles of every anime, built on first use.
    /// </summary>
    /// <returns>The titles, by AniDB anime ID.</returns>
    private ConcurrentDictionary<int, string[]> Titles()
    {
        if (Volatile.Read(ref _titles) is { } built)
            return built;

        lock (_buildLock)
        {
            if (_titles is not null)
                return _titles;

            var titles = new ConcurrentDictionary<int, string[]>();
            foreach (var (entity, _) in _cache.Enumerate())
                if (AnimeIDOf(entity) is { } animeID && Read(entity) is { Length: > 0 } read)
                    titles[animeID] = read;

            Volatile.Write(ref _titles, titles);
            return titles;
        }
    }

    /// <summary>
    ///   Reads an anime's titles again after a write changed them.
    /// </summary>
    /// <param name="sender">The text cache.</param>
    /// <param name="changes">What changed, per entry.</param>
    private void OnTextsChanged(object? sender, IReadOnlyList<TextEntityChange> changes)
    {
        lock (_buildLock)
        {
            if (_titles is not { } titles)
                return;

            foreach (var change in changes)
            {
                if (!change.Kinds.Contains(TextKind.Title) || AnimeIDOf(change.EntityID) is not { } animeID)
                    continue;

                if (Read(change.EntityID) is { Length: > 0 } read)
                    titles[animeID] = read;
                else
                    titles.TryRemove(animeID, out _);
            }
        }
    }

    /// <summary>
    ///   The normalized main and official titles AniDB gave an anime.
    /// </summary>
    /// <param name="entity">The anime.</param>
    /// <returns>The titles.</returns>
    private string[] Read(MetadataGuid entity)
        => [.. _cache.GetTitles(entity, MetadataSource.AniDB, isEnabled: true)
            .Where(title => title.Type is TitleType.Main or TitleType.Official)
            .Select(title => NormalizeForSearch(title.Value))];

    /// <summary>
    ///   The AniDB anime an entry is, if it is one.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <returns>The anime ID, or <c>null</c> for any other entry.</returns>
    private static int? AnimeIDOf(MetadataGuid entity)
        => entity.Source == MetadataSource.AniDB && entity.EntityType == MetadataEntityType.Series &&
           int.TryParse(entity.ID, NumberStyles.None, CultureInfo.InvariantCulture, out var animeID)
            ? animeID
            : null;

    #endregion
}
