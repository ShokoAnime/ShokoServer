using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Utilities;

using FileCrossReference = Shoko.Server.API.v3.Models.Shoko.FileCrossReference;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.Services;

public class GeneratedPlaylistService
{
    private readonly HttpContext _context;

    private readonly AnimeSeriesService _animeSeriesService;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly AnimeEpisodeRepository _episodeRepository;

    private readonly VideoLocalRepository _videoRepository;

    public GeneratedPlaylistService(IHttpContextAccessor contentAccessor, AnimeSeriesService animeSeriesService, AnimeSeriesRepository seriesRepository, AnimeEpisodeRepository episodeRepository, VideoLocalRepository videoRepository)
    {
        _context = contentAccessor.HttpContext!;
        _animeSeriesService = animeSeriesService;
        _seriesRepository = seriesRepository;
        _episodeRepository = episodeRepository;
        _videoRepository = videoRepository;
    }

    public bool TryParsePlaylist(string[] items, out IReadOnlyList<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> playlist, ModelStateDictionary? modelState = null, string fieldName = "playlist")
    {
        modelState ??= new();
        playlist = ParsePlaylist(items, modelState, fieldName);
        return modelState.IsValid;
    }

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
            var subItems = item.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (subItems.Any(subItem => subItem[0] == 's'))
            {
                var seriesItem = subItems.First(subItem => subItem[0] == 's');
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

                var endIndex = seriesItem.IndexOf('+');
                if (endIndex == -1)
                    endIndex = seriesItem.Length;
                var plusExtras = endIndex == seriesItem.Length ? [] : seriesItem[(endIndex + 1)..].Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!int.TryParse(seriesItem[1..endIndex], out var seriesID) || seriesID <= 0)
                {
                    modelState?.AddModelError($"{fieldName}[{index}]", $"Invalid series ID \"{item}\".");
                    continue;
                }
                if (_seriesRepository.GetByAnimeID(seriesID) is not { } series)
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
                        if (_episodeRepository.GetByAniDBEpisodeID(episodeID) is not { } extraEpisode)
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
                            if ((fileSize > 0 ? _videoRepository.GetByHashAndSize(ed2kHash, fileSize) : _videoRepository.GetByHash(ed2kHash)) is not { } video0)
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
                        if (_videoRepository.GetByID(fileID) is not { } video)
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
        var user = _context.GetUser();
        var episodes = _animeSeriesService.GetNextUpEpisodes((series as SVR_AnimeSeries)!, user.JMMUserID, options);

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
        var videos = xrefs.Select(xref => _videoRepository.GetByHashAndSize(xref.ED2K, xref.FileSize))
            .WhereNotNull()
            .ToList();
        yield return ([episode], videos);
    }

    private IEnumerable<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> GetListForVideo(IVideo video)
    {
        var episode = video.Episodes
            .OrderBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .FirstOrDefault();
        return episode is not null ? [([episode], [video])] : [];
    }

    public FileStreamResult GeneratePlaylist(
        IEnumerable<(IReadOnlyList<IShokoEpisode> episodes, IReadOnlyList<IVideo> videos)> playlist,
        string name = "Playlist"
    )
    {
        var m3U8 = new StringBuilder("#EXTM3U\n");
        var request = _context.Request;
        var uri = new UriBuilder(
            request.Scheme,
            request.Host.Host,
            request.Host.Port ?? (request.Scheme == "https" ? 443 : 80),
            request.PathBase,
            null
        );
        foreach (var (episodes, videos) in playlist)
        {
            var series = episodes[0].Series;
            if (series is null)
                continue;

            var index = 0;
            foreach (var video in videos)
                m3U8.Append(GetEpisodeEntry(new UriBuilder(uri.ToString()), series, episodes[0], video, ++index, videos.Count, episodes.Count));
        }

        var bytes = Encoding.UTF8.GetBytes(m3U8.ToString());
        var stream = new MemoryStream(bytes);
        return new FileStreamResult(stream, "application/x-mpegURL")
        {
            FileDownloadName = $"{name}.m3u8",
        };
    }

    private static string GetEpisodeEntry(UriBuilder uri, IShokoSeries series, IShokoEpisode episode, IVideo video, int part, int totalParts, int episodeRange)
    {
        var poster = series.GetPreferredImageForType(ImageEntityType.Poster) ?? series.DefaultPoster;
        var parts = totalParts > 1 ? $" ({part}/{totalParts})" : string.Empty;
        var episodeNumber = episode.Type is EpisodeType.Episode
            ? episode.EpisodeNumber.ToString()
            : $"{episode.Type.ToString()[0]}{episode.EpisodeNumber}";
        var episodePartNumber = totalParts > 1 ? $".{part}" : string.Empty;
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("shokoVersion", Utils.GetApplicationVersion());

        // These fields are for media player plugins to consume.
        if (poster is not null && !string.IsNullOrEmpty(poster.RemoteURL))
            queryString.Add("posterUrl", poster.RemoteURL);
        queryString.Add("appId", "07a58b50-5109-5aa3-abbc-782fed0df04f"); // plugin id
        queryString.Add("animeId", series.AnidbAnimeID.ToString());
        queryString.Add("animeName", series.PreferredTitle);
        queryString.Add("epId", episode.AnidbEpisodeID.ToString());
        queryString.Add("episodeName", episode.PreferredTitle);
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
        return $"#EXTINF:-1,{series.PreferredTitle} - {episodeNumber}{episodePartNumber} - {episode.PreferredTitle}{parts}\n{uri}\n";
    }
}
