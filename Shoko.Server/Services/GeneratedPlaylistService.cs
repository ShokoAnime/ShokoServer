using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Server.API;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;

using FileCrossReference = Shoko.Server.API.v3.Models.Shoko.FileCrossReference;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.Services;

public class GeneratedPlaylistService(
    ISystemService systemService,
    IImageManager imageManager,
    IHttpContextAccessor contextAccessor,
    AnimeGroupRepository groupRepository,
    AnimeSeriesService animeSeriesService,
    AnimeSeriesRepository seriesRepository,
    AnimeEpisodeRepository episodeRepository,
    VideoLocalRepository videoRepository,
    AuthTokensRepository authTokensRepository
)
{
    /// <summary>
    /// Attempts to parse a playlist from the given DSL items. Returns <c>true</c> if valid.
    /// See <see cref="ParsePlaylist"/> for the DSL specification.
    /// </summary>
    /// <param name="items">Comma-separated playlist entries. Each entry is sub-item split by <c>+</c> or space.</param>
    /// <param name="playlist">The parsed playlist tuples: episode groups and their associated video files.</param>
    /// <param name="modelState">Optional model state dictionary to collect validation errors.</param>
    /// <param name="fieldName">The field name to use for error keys. Defaults to <c>"playlist"</c>.</param>
    /// <returns><c>true</c> if the playlist is valid; otherwise <c>false</c>.</returns>
    public bool TryParsePlaylist(string[] items, out IReadOnlyList<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> playlist, ModelStateDictionary? modelState = null, string fieldName = "playlist")
    {
        modelState ??= new();
        playlist = ParsePlaylist(items, modelState, fieldName);
        return modelState.IsValid;
    }

    /// <summary>
    ///   Parses a playlist from the given DSL items.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Playlist item DSL. Each string in items is a comma-separated entry.
    ///   Within an entry, sub-items are separated by <c>+</c> or a space.
    /// </para>
    /// <para>
    ///   Prefixes:
    ///     <c>g</c> = group (Shoko AnimeGroup ID). Follows prequel/sequel chain within the group.
    ///     <c>a</c> = series (AniDB Anime ID),
    ///     <c>s</c> = series (Shoko AnimeSeries ID),
    ///     <c>e</c> = episode (AniDB Episode ID),
    ///     <c>E</c> = episode (Shoko AnimeEpisode ID),
    ///     <c>f</c> = file (ED2K hash),
    ///     <c>r</c> = release group filter (AniDB Release Group ID).
    ///
    ///   A bare 32+ hex chars is treated as an ED2K hash.
    ///   A bare number is a Shoko VideoLocal file ID.
    /// </para>
    /// <para>
    ///   Release group interaction:
    ///     <c>a id r gid</c> (or <c>s id r gid</c>) filters series files to a release group.
    ///     <c>r gid e id</c> (or <c>E id</c>) filters episode files to a release group.
    ///     <c>r gid f hash</c> is an error.
    ///     <c>r gid</c> alone is a no-op.
    ///
    ///   In explicit <c>e id f hash</c> (or <c>E id f hash</c>) pairs, r is ignored.
    ///   Without r, the most-used group is auto-selected.
    /// </para>
    /// <para>
    ///   Series extras (appended after <c>+</c>, dash-separated):
    ///     <c>a id+onlyUnwatched</c> (or <c>s id+...</c>),
    ///     <c>includeSpecials</c>,
    ///     <c>includeOthers</c>,
    ///     <c>includeRewatching</c>.
    /// </para>
    /// <para>
    ///   Group extras (appended after <c>+</c>, dash-separated):
    ///     Same as series plus:
    ///     <c>recursive</c> — include series from child groups,
    ///     <c>includeAllSeries</c> — skip chain, include all series by air date (implies recursive),
    ///     <c>includePrequels</c> — walk prequel relations backward too (no-op if includeAllSeries).
    /// </para>
    /// <para>
    ///   Examples:
    ///     <c>a123</c> = next-up for anime 123 (AniDB ID).
    ///     <c>s456</c> = next-up for Shoko series 456.
    ///     <c>a123 r789</c> = same, group 789 only.
    ///     <c>a123+onlyUnwatched</c> = unwatched only.
    ///     <c>g123</c> = chain for Shoko group 123 (direct children, sequels only).
    ///     <c>g123+recursive</c> = chain including child groups.
    ///     <c>g123+includePrequels</c> = chain walking prequels backward too.
    ///     <c>g123+includeAllSeries</c> = all series in group, air date order.
    ///     <c>g123+includeAllSeries-onlyUnwatched</c> = all series, unwatched only.
    ///     <c>e98765</c> = episode 98765 (AniDB Episode ID).
    ///     <c>E54321</c> = episode 54321 (Shoko AnimeEpisode ID).
    ///     <c>r789 e98765</c> = episode 98765, group 789.
    ///     <c>42</c> = file by VideoLocal ID.
    ///     <c>hash</c> = file by ED2K.
    ///     <c>hash-size</c> = file by ED2K+size.
    /// </para>
    /// </remarks>
    /// <param name="items">
    ///   Comma-separated playlist entries. Within each entry sub-items are
    ///   split by <c>+</c> or space.
    /// </param>
    /// <param name="modelState">
    ///   Optional model state dictionary to collect validation errors.
    /// </param>
    /// <param name="fieldName">
    ///   The field name to use for error keys. Defaults to <c>"playlist"</c>.
    /// </param>
    /// <returns>
    ///   The parsed playlist as a list of episode-group/video-file tuples.
    /// </returns>
    public IReadOnlyList<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> ParsePlaylist(string[] items, ModelStateDictionary? modelState = null, string fieldName = "playlist")
    {
        items ??= [];
        var playlist = new List<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)>();
        var index = -1;
        foreach (var item in items)
        {
            index++;
            if (string.IsNullOrEmpty(item))
                continue;

            var releaseGroupID = -2;
            var subItems = item.Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (subItems.Any(subItem => subItem[0] is 'g'))
            {
                var groupItem = subItems.First(subItem => subItem[0] is 'g');
                var releaseItem = subItems.FirstOrDefault(subItem => subItem[0] == 'r');
                if (releaseItem is not null)
                {
                    if (subItems.Length > 2)
                    {
                        modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid item \"{item}\".");
                        continue;
                    }

                    if (!int.TryParse(releaseItem[1..], out releaseGroupID) || releaseGroupID <= 0)
                    {
                        modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid release group ID \"{releaseItem}\".");
                        continue;
                    }
                }
                else if (subItems.Length > 1)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid item \"{item}\".");
                    continue;
                }

                var endIndex = groupItem.IndexOf(['+', ' ']);
                if (endIndex == -1)
                    endIndex = groupItem.Length;
                var plusExtras = endIndex == groupItem.Length ? [] : groupItem[(endIndex + 1)..].Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!int.TryParse(groupItem[1..endIndex], out var groupID) || groupID <= 0)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid group ID \"{item}\".");
                    continue;
                }
                if (groupRepository.GetByID(groupID) is not { } group)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Unknown group ID \"{item}\".");
                    continue;
                }

                // Parse group-specific extras.
                var onlyUnwatched = false;
                var includeSpecials = false;
                var includeOthers = false;
                var includeRewatching = false;
                var recursive = false;
                var includeAllSeries = false;
                var includePrequels = false;
                if (plusExtras.Length > 0)
                {
                    if (plusExtras.Contains("onlyUnwatched"))
                        onlyUnwatched = true;
                    if (plusExtras.Contains("includeSpecials"))
                        includeSpecials = true;
                    if (plusExtras.Contains("includeOthers"))
                        includeOthers = true;
                    if (plusExtras.Contains("includeRewatching"))
                        includeRewatching = true;
                    if (plusExtras.Contains("recursive"))
                        recursive = true;
                    if (plusExtras.Contains("includeAllSeries"))
                        includeAllSeries = true;
                    if (plusExtras.Contains("includePrequels"))
                        includePrequels = true;
                }

                foreach (var tuple in GetListForGroup(
                    group,
                    releaseGroupID,
                    new()
                    {
                        OnlyUnwatched = onlyUnwatched,
                        IncludeSpecials = includeSpecials,
                        IncludeOthers = includeOthers,
                        IncludeRewatching = includeRewatching,
                        Recursive = recursive,
                        IncludeAllSeries = includeAllSeries,
                        IncludePrequels = includePrequels,
                    }
                ))
                    playlist.Add(tuple);

                continue;
            }

            if (subItems.Any(subItem => subItem[0] is 'a' or 's'))
            {
                var seriesItem = subItems.First(subItem => subItem[0] is 'a' or 's');
                var releaseItem = subItems.FirstOrDefault(subItem => subItem[0] == 'r');
                if (releaseItem is not null)
                {
                    if (subItems.Length > 2)
                    {
                        modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid item \"{item}\".");
                        continue;
                    }

                    if (!int.TryParse(releaseItem[1..], out releaseGroupID) || releaseGroupID <= 0)
                    {
                        modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid release group ID \"{releaseItem}\".");
                        continue;
                    }
                }
                else if (subItems.Length > 1)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid item \"{item}\".");
                    continue;
                }

                var endIndex = seriesItem.IndexOf(['+', ' ']);
                if (endIndex == -1)
                    endIndex = seriesItem.Length;
                var plusExtras = endIndex == seriesItem.Length ? [] : seriesItem[(endIndex + 1)..].Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!int.TryParse(seriesItem[1..endIndex], out var seriesID) || seriesID <= 0)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid series ID \"{item}\".");
                    continue;
                }
                var series = seriesItem[0] is 's'
                    ? seriesRepository.GetByID(seriesID)
                    : seriesRepository.GetByAnimeID(seriesID);
                if (series is null)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Unknown series ID \"{item}\".");
                    continue;
                }

                // Check if we've included any extra options.
                var onlyUnwatched = false;
                var includeSpecials = false;
                var includeOthers = false;
                var includeRewatching = false;
                if (plusExtras.Length > 0)
                {
                    if (plusExtras.Contains("onlyUnwatched"))
                        onlyUnwatched = true;
                    if (plusExtras.Contains("includeSpecials"))
                        includeSpecials = true;
                    if (plusExtras.Contains("includeOthers"))
                        includeOthers = true;
                    if (plusExtras.Contains("includeRewatching"))
                        includeRewatching = true;
                }

                // Get the playlist items for the series.
                foreach (var tuple in GetListForSeries(
                    series,
                    releaseGroupID,
                    new()
                    {
                        IncludeCurrentlyWatching = !onlyUnwatched,
                        IncludeSpecials = includeSpecials,
                        IncludeOthers = includeOthers,
                        IncludeRewatching = includeRewatching,
                    }
                ))
                    playlist.Add(tuple);

                continue;
            }

            var offset = -1;
            var episodes = new List<IShokoEpisode>();
            var videos = new List<IVideo>();
            foreach (var subItem in subItems)
            {
                offset++;
                var rawValue = subItem;
                switch (subItem[0])
                {
                    case 'r':
                    {
                        if (releaseGroupID is not -2)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Unexpected release group ID \"{rawValue}\" at index {index} at offset {offset}");
                            continue;
                        }
                        if (!int.TryParse(rawValue[1..], out releaseGroupID) || releaseGroupID < -1)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Invalid release group ID \"{rawValue}\" at index {index} at offset {offset}");
                            continue;
                        }
                        break;
                    }

                    case 'e':
                    {
                        if (!int.TryParse(rawValue[1..], out var episodeID) || episodeID <= 0)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Invalid episode ID \"{rawValue}\" at index {index} at offset {offset}");
                            continue;
                        }
                        if (episodeRepository.GetByAniDBEpisodeID(episodeID) is not { } extraEpisode)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Unknown episode ID \"{rawValue}\" at index {index} at offset {offset}");
                            continue;
                        }
                        episodes.Add(extraEpisode);
                        break;
                    }

                    case 'E':
                    {
                        if (!int.TryParse(rawValue[1..], out var episodeID) || episodeID <= 0)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Invalid episode ID \"{rawValue}\" at index {index} at offset {offset}");
                            continue;
                        }
                        if (episodeRepository.GetByID(episodeID) is not { } extraEpisode)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Unknown episode ID \"{rawValue}\" at index {index} at offset {offset}");
                            continue;
                        }
                        episodes.Add(extraEpisode);
                        break;
                    }

                    case 'f':
                        rawValue = rawValue[1..];
                        goto default;

                    default:
                    {
                        // Lookup by ED2K (optionally also by file size)
                        if (rawValue.Length >= 32)
                        {
                            var ed2kHash = rawValue[0..32];
                            var fileSize = 0L;
                            if (rawValue[32] == '-')
                            {
                                if (!long.TryParse(rawValue[33..], out fileSize) || fileSize <= 0)
                                {
                                    modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Invalid file size \"{rawValue}\" at index {index} at offset {offset}");
                                    continue;
                                }
                            }
                            if ((fileSize > 0 ? videoRepository.GetByEd2kAndSize(ed2kHash, fileSize) : videoRepository.GetByEd2k(ed2kHash)) is not { } video0)
                            {
                                if (fileSize == 0)
                                    modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Unknown hash \"{rawValue}\" at index {index} at offset {offset}");
                                else
                                    modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Unknown hash/size pair \"{rawValue}\" at index {index} at offset {offset}");
                                continue;
                            }
                            videos.Add(video0);
                            continue;
                        }

                        // Lookup by file ID
                        if (!int.TryParse(rawValue, out var fileID) || fileID <= 0)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Invalid file ID \"{rawValue}\".");
                            continue;
                        }
                        if (videoRepository.GetByID(fileID) is not { } video)
                        {
                            modelState?.AddModelError($"{fieldName}[{index}][{offset}]", $"Unknown file ID \"{rawValue}\".");
                            continue;
                        }
                        videos.Add(video);
                        break;
                    }
                }
            }

            // Make sure all the videos and episodes are connected for each item.
            // This will generally allow 1-N and N-1 relationships, but not N-N relationships.
            foreach (var video in videos)
            {
                foreach (var episode in episodes)
                {
                    if (video.Episodes.Any(x => x.ID == episode.ID))
                        continue;
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Video ID \"{video.ID}\" does not belong to episode ID \"{episode.AnidbEpisodeID}\".");
                    continue;
                }
            }

            // Skip adding it to the playlist if it's empty.
            if (episodes.Count == 0 && videos.Count == 0)
                continue;

            // Add video to playlist.
            if (episodes.Count is 0)
            {
                if (releaseGroupID is not -2)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", "Cannot specify a release group ID for a video.");
                    continue;
                }

                if (videos.Count > 1)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", "Cannot combine multiple videos.");
                    continue;
                }

                foreach (var tuple in GetListForVideo(videos[0]))
                    playlist.Add(tuple);
                continue;
            }

            // Add episode to playlist.
            if (videos.Count is 0)
            {
                if (episodes.Count > 1)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", "Cannot combine multiple episodes.");
                    continue;
                }

                foreach (var tuple in GetListForEpisode(episodes[0], releaseGroupID))
                    playlist.Add(tuple);
                continue;
            }

            // Add video and episode combination to the playlist.
            playlist.Add((episodes, videos));
        }

        // Combine episodes with the same video into a single playlist entry.
        index = 1;
        while (index < playlist.Count)
        {
#pragma warning disable IDE0042
            var current = playlist[index];
            var previous = playlist[index - 1];
#pragma warning restore IDE0042
            if (previous.videos.Count is 1 && current.videos.Count is 1 && previous.videos[0].ID == current.videos[0].ID)
            {
                previous.episodes = [.. previous.episodes, .. current.episodes];
                playlist.RemoveAt(index);
                continue;
            }

            index++;
        }

        return playlist;
    }

    private IEnumerable<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> GetListForSeries(IShokoSeries series, int? releaseGroupID = null, AnimeSeriesService.NextUpQueryOptions? options = null)
    {
        options ??= new();
        options.IncludeMissing = false;
        options.IncludeUnaired = false;
        var user = contextAccessor.HttpContext.GetUser();
        var episodes = animeSeriesService.GetNextUpEpisodes((series as AnimeSeries)!, user.JMMUserID, options);

        // Make sure the release group is in the list, otherwise pick the most used group.
        var xrefs = FileCrossReference.From(series.CrossReferences).FirstOrDefault(seriesXRef => seriesXRef.SeriesID.ID == series.ID)?.EpisodeIDs ?? [];
        var releaseGroups = xrefs.GroupBy(xref => xref.ReleaseGroup ?? -1).ToDictionary(xref => xref.Key, xref => xref.Count());
        if (releaseGroups.Count > 0 && (releaseGroupID is null || !releaseGroups.ContainsKey(releaseGroupID.Value)))
            releaseGroupID = releaseGroups.MaxBy(xref => xref.Value).Key;
        if (releaseGroupID is -1)
            releaseGroupID = null;

        foreach (var episode in episodes)
            foreach (var tuple in GetListForEpisode(episode, releaseGroupID))
                yield return tuple;
    }

    private record GroupQueryOptions
    {
        public bool OnlyUnwatched { get; init; }
        public bool IncludeSpecials { get; init; }
        public bool IncludeOthers { get; init; }
        public bool IncludeRewatching { get; init; }
        public bool Recursive { get; init; }
        public bool IncludeAllSeries { get; init; }
        public bool IncludePrequels { get; init; }
    }

    private IEnumerable<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> GetListForGroup(IShokoGroup group, int? releaseGroupID = null, GroupQueryOptions? options = null)
    {
        options ??= new();

        var seriesList = options.Recursive || options.IncludeAllSeries
            ? group.AllSeries
            : group.Series;

        IReadOnlyList<IShokoSeries> orderedSeries;
        if (options.IncludeAllSeries)
        {
            orderedSeries = seriesList;
        }
        else
        {
            orderedSeries = BuildSeriesChain(seriesList, options.IncludePrequels);
        }

        var user = contextAccessor.HttpContext.GetUser();
        foreach (var series in orderedSeries)
        {
            if (series is not AnimeSeries animeSeries)
                continue;

            var episodes = animeSeriesService.GetNextUpEpisodes(
                animeSeries,
                user.JMMUserID,
                new()
                {
                    IncludeCurrentlyWatching = !options.OnlyUnwatched,
                    IncludeSpecials = options.IncludeSpecials,
                    IncludeOthers = options.IncludeOthers,
                    IncludeRewatching = options.IncludeRewatching,
                    IncludeMissing = false,
                    IncludeUnaired = false,
                }
            );

            foreach (var episode in episodes)
                foreach (var tuple in GetListForEpisode(episode, releaseGroupID))
                    yield return tuple;
        }
    }

    private static IReadOnlyList<IShokoSeries> BuildSeriesChain(IReadOnlyList<IShokoSeries> seriesList, bool includePrequels)
    {
        if (seriesList.Count <= 1)
            return seriesList;

        var seriesByAnimeId = seriesList.ToDictionary(s => s.AnidbAnimeID);

        // Build a directed graph of prequel/sequel relations within the group.
        // BaseID → RelatedID is always forward in viewing order.
        var nextMap = new Dictionary<int, List<int>>();
        var prevMap = new Dictionary<int, List<int>>();

        foreach (var series in seriesList)
        {
            foreach (var relation in series.RelatedSeries)
            {
                if (relation.RelationType is not (RelationType.Prequel or RelationType.Sequel))
                    continue;

                if (!seriesByAnimeId.ContainsKey(relation.RelatedID))
                    continue;

                if (!nextMap.TryGetValue(relation.BaseID, out var nextList))
                {
                    nextList = [];
                    nextMap[relation.BaseID] = nextList;
                }
                nextList.Add(relation.RelatedID);

                if (!prevMap.TryGetValue(relation.RelatedID, out var prevList))
                {
                    prevList = [];
                    prevMap[relation.RelatedID] = prevList;
                }
                prevList.Add(relation.BaseID);
            }
        }

        // Find root: the starting point of the chain.
        int rootId;
        if (includePrequels)
        {
            // Earliest airing series, walking both directions.
            rootId = seriesList.OrderBy(s => s.AirDate?.ToDateTime() ?? DateTime.MaxValue).First().AnidbAnimeID;
        }
        else
        {
            // Series with no predecessor in the group; fall back to earliest airing.
            var candidates = seriesList.Where(s => !prevMap.ContainsKey(s.AnidbAnimeID)).ToList();
            rootId = candidates.Count > 0
                ? candidates.OrderBy(s => s.AirDate?.ToDateTime() ?? DateTime.MaxValue).First().AnidbAnimeID
                : seriesList.OrderBy(s => s.AirDate?.ToDateTime() ?? DateTime.MaxValue).First().AnidbAnimeID;
        }

        // Walk forward (sequels).
        var forwardChain = new List<int>();
        var visited = new HashSet<int>();
        var current = rootId;
        while (current != 0 && visited.Add(current))
        {
            forwardChain.Add(current);
            if (!nextMap.TryGetValue(current, out var successors) || successors.Count == 0)
                break;

            current = PickBest(successors, seriesByAnimeId);
        }

        // Walk backward (prequels), if requested.
        if (includePrequels)
        {
            var backwardChain = new List<int>();
            current = rootId;
            var backwardVisited = new HashSet<int>();
            while (current != 0 && backwardVisited.Add(current))
            {
                if (!prevMap.TryGetValue(current, out var predecessors) || predecessors.Count == 0)
                    break;

                current = PickBest(predecessors, seriesByAnimeId);
                backwardChain.Add(current);
            }

            backwardChain.Reverse();
            forwardChain = [.. backwardChain, .. forwardChain];
        }

        return forwardChain.Select(id => seriesByAnimeId[id]).ToList();
    }

    private static int PickBest(IReadOnlyList<int> candidates, Dictionary<int, IShokoSeries> seriesByAnimeId)
    {
        return candidates
            .Select(id => seriesByAnimeId[id])
            .OrderByDescending(s => s.EpisodeCounts.Episodes)
            .ThenBy(s => (int)s.Type)
            .First()
            .AnidbAnimeID;
    }

    private IEnumerable<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> GetListForEpisode(IShokoEpisode episode, int? releaseGroupID = null)
    {
        // For now we're just re-using the logic used in the API layer. In the future it should be moved to the service layer or somewhere else.
        var xrefs = FileCrossReference.From(episode.CrossReferences).FirstOrDefault(seriesXRef => seriesXRef.SeriesID.ID == episode.SeriesID)?.EpisodeIDs ?? [];
        if (xrefs.Count is 0)
            yield break;

        // Make sure the release group is in the list, otherwise pick the most used group.
        var releaseGroups = xrefs.GroupBy(xref => xref.ReleaseGroup ?? -1).ToDictionary(xref => xref.Key, xref => xref.Count());
        if (releaseGroups.Count > 0 && (releaseGroupID is null || !releaseGroups.ContainsKey(releaseGroupID.Value)))
            releaseGroupID = releaseGroups.MaxBy(xref => xref.Value).Key;
        if (releaseGroupID is -1)
            releaseGroupID = null;

        // Filter to only cross-references which from the specified release group.
        xrefs = xrefs
            .Where(xref => xref.ReleaseGroup == releaseGroupID)
            .ToList();
        var videos = xrefs.Select(xref => videoRepository.GetByEd2kAndSize(xref.ED2K, xref.FileSize))
            .WhereNotNull()
            .ToList();
        yield return ([episode], videos);
    }

    private static readonly EpisodeType[] _videoToEpisodeGroupPreference = [EpisodeType.Episode, EpisodeType.Special, EpisodeType.Other, EpisodeType.Credits, EpisodeType.Trailer, EpisodeType.Parody];

    private IEnumerable<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> GetListForVideo(IVideo video)
    {
        var crossReferences = video.CrossReferences;
        if (crossReferences.Count is 0)
            return [];

        if (crossReferences.Count is 1)
        {
            if (crossReferences[0].ShokoEpisode is not { } episode)
                return [];

            return [([episode], [video])];
        }

        var seriesOrder = crossReferences
            .Select(xref => xref.AnidbAnimeID)
            .Distinct()
            .ToArray();
        var episodes = crossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .Select(xref => (xref, episode: xref.ShokoEpisode!))
            .Where(tuple => tuple.episode is not null)
            .GroupBy(tuple => tuple.episode.Type)
            .OrderBy(groupBy => Array.IndexOf(_videoToEpisodeGroupPreference, groupBy.Key))
            .First()
            .OrderBy(tuple => Array.IndexOf(seriesOrder, tuple.xref.AnidbAnimeID))
            .ThenBy(tuple => tuple.episode.EpisodeNumber)
            .Select(tuple => tuple.episode)
            .ToList() as IReadOnlyList<IShokoEpisode>;
        return episodes is { Count: > 0 } ? [(episodes, [video])] : [];
    }

    /// <summary>
    /// Generates an M3U8 playlist file from the given playlist tuples.
    /// Each video entry includes the streaming URL with an API key, series/episode metadata,
    /// and poster artwork URL for media player plugin consumption.
    /// </summary>
    /// <param name="playlist">Parsed playlist tuples from <see cref="ParsePlaylist"/>.</param>
    /// <param name="name">The output file name (without extension). Defaults to <c>"Playlist"</c>.</param>
    /// <returns>A <c>.m3u8</c> file stream result.</returns>
    public FileStreamResult GeneratePlaylist(
        IEnumerable<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> playlist,
        string name = "Playlist"
    )
    {
        var m3U8 = new StringBuilder("#EXTM3U\n");
        var request = contextAccessor.HttpContext!.Request;
        var uri = new UriBuilder(
            request.Scheme,
            request.Host.Host,
            request.Host.Port ?? (request.Scheme == "https" ? 443 : 80),
            request.PathBase,
            null
        );
        // Get the API Key from the request or generate a new one to use for the playlist.
        var apiKey = request.Query["apikey"].FirstOrDefault() ?? request.Headers["apikey"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            var user = contextAccessor.HttpContext.GetUser();
            apiKey = authTokensRepository.CreateNewApiKey(user, "playlist");
        }
        foreach (var (episodes, videos) in playlist)
        {
            var series = episodes[0].Series;
            if (series is null)
                continue;

            var index = 0;
            foreach (var video in videos)
                m3U8.Append(GetEpisodeEntry(new UriBuilder(uri.ToString()), series, episodes[0], video, ++index, videos.Count, episodes.Count, apiKey));
        }

        var bytes = Encoding.UTF8.GetBytes(m3U8.ToString());
        var stream = new MemoryStream(bytes);
        return new FileStreamResult(stream, "application/x-mpegURL")
        {
            FileDownloadName = $"{name}.m3u8",
        };
    }

    private string GetEpisodeEntry(UriBuilder uri, IShokoSeries series, IShokoEpisode episode, IVideo video, int part, int totalParts, int episodeRange, string apiKey)
    {
        var poster = series.GetPreferredImageForType(ImageEntityType.Primary) ?? series.DefaultPrimaryImage;
        var parts = totalParts > 1 ? $" ({part}/{totalParts})" : string.Empty;
        var episodeNumber = episode.Type is EpisodeType.Episode
            ? episode.EpisodeNumber.ToString()
            : $"{episode.Type.ToString()[0]}{episode.EpisodeNumber}";
        var episodePartNumber = totalParts > 1 ? $".{part}" : string.Empty;
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("shokoVersion", systemService.Version.Version.ToSemanticVersioningString());
        queryString.Add("apikey", apiKey);

        // These fields are for media player plugins to consume.
        if (poster is not null && imageManager.GetTemplateUrlForSource(poster.Source) is { } template)
            queryString.Add("posterUrl", string.Format(template, poster.ResourceID));
        queryString.Add("appId", "07a58b50-5109-5aa3-abbc-782fed0df04f"); // plugin id
        queryString.Add("animeId", series.AnidbAnimeID.ToString());
        queryString.Add("animeName", series.Title);
        queryString.Add("epId", episode.AnidbEpisodeID.ToString());
        queryString.Add("episodeName", episode.Title);
        queryString.Add("epNo", episodeNumber);
        queryString.Add("epNoRange", episodeRange.ToString());
        queryString.Add("epCount", series.EpisodeCounts.Episodes.ToString());
        if (totalParts > 1)
        {
            queryString.Add("epNoPart", part.ToString());
            queryString.Add("epNoPartCount", totalParts.ToString());
        }
        queryString.Add("restricted", series.Restricted ? "true" : "false");

        uri.Path = $"{(uri.Path.Length > 1 ? uri.Path + "/" : "/")}api/v3/File/{video.ID}/Stream";
        uri.Query = queryString.ToString();
        return $"#EXTINF:-1,{series.Title} - {episodeNumber}{episodePartNumber} - {episode.Title}{parts}\n{uri.Uri}\n";
    }
}
