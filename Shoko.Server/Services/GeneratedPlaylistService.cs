using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Utilities;

using FileCrossReference = Shoko.Server.API.v3.Models.Shoko.FileCrossReference;

#nullable enable
namespace Shoko.Server.Services;

public class GeneratedPlaylistService
{
    private readonly HttpContext _context;

    private readonly AnimeSeriesService _animeSeriesService;

    private readonly VideoLocalRepository _videoLocalRepository;

    public GeneratedPlaylistService(IHttpContextAccessor contentAccessor, AnimeSeriesService animeSeriesService, VideoLocalRepository videoLocalRepository)
    {
        _context = contentAccessor.HttpContext!;
        _animeSeriesService = animeSeriesService;
        _videoLocalRepository = videoLocalRepository;
    }

    public IEnumerable<(IShokoEpisode ep, IReadOnlyList<IVideo> videos)> GetListForSeries(IShokoSeries series, int? releaseGroupID = null, AnimeSeriesService.NextUpQueryOptions? options = null)
    {
        options ??= new();
        options.IncludeMissing = false;
        options.IncludeUnaired = false;
        var user = _context.GetUser();
        var episodes = _animeSeriesService.GetNextUpEpisodes((series as SVR_AnimeSeries)!, user.JMMUserID, options);

        // Make sure the release group is in the list, otherwise pick the most used group.
        var xrefs = FileCrossReference.From(series.CrossReferences).FirstOrDefault(seriesXRef => seriesXRef.SeriesID.ID == series.ID)?.EpisodeIDs ?? [];
        var releaseGroups = xrefs.Select(xref => xref.ReleaseGroup ?? -1).GroupBy(xref => xref).ToDictionary(xref => xref.Key, xref => xref.Count());
        if (releaseGroupID is null || !releaseGroups.ContainsKey(releaseGroupID.Value))
            releaseGroupID = releaseGroups.MaxBy(xref => xref.Value).Key;
        if (releaseGroupID is -1)
            releaseGroupID = null;

        foreach (var episode in episodes)
            foreach (var tuple in GetListForEpisode(episode, releaseGroupID))
                yield return tuple;
    }

    public IEnumerable<(IShokoEpisode ep, IReadOnlyList<IVideo> videos)> GetListForEpisode(IShokoEpisode episode, int? releaseGroupID = null)
    {
        // For now we're just re-using the logic used in the API layer. In the future it should be moved to the service layer or somewhere else.
        var xrefs = FileCrossReference.From(episode.CrossReferences).FirstOrDefault(seriesXRef => seriesXRef.SeriesID.ID == episode.SeriesID)?.EpisodeIDs ?? [];
        if (xrefs.Count is 0)
            yield break;

        // Make sure the release group is in the list, otherwise pick the most used group.
        var releaseGroups = xrefs.Select(xref => xref.ReleaseGroup ?? -1).GroupBy(xref => xref).ToDictionary(xref => xref.Key, xref => xref.Count());
        if (releaseGroupID is null || !releaseGroups.ContainsKey(releaseGroupID.Value))
            releaseGroupID = releaseGroups.MaxBy(xref => xref.Value).Key;
        if (releaseGroupID is -1)
            releaseGroupID = null;

        // Filter to only cross-references which from the specified release group.
        xrefs = xrefs
            .Where(xref => xref.ReleaseGroup == releaseGroupID)
            .ToList();
        var videos = xrefs.Select(xref => _videoLocalRepository.GetByHashAndSize(xref.ED2K, xref.FileSize))
            .WhereNotNull()
            .ToList();
        yield return (episode, videos);
    }

    public IEnumerable<(IShokoEpisode ep, IReadOnlyList<IVideo> videos)> GetListForVideo(IVideo video)
    {
        var episode = video.Episodes
            .OrderBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .FirstOrDefault();
        return episode is not null ? [(episode, [video])] : [];
    }

    public IEnumerable<(IShokoEpisode ep, IReadOnlyList<IVideo> videos)> GetListForVideos(IEnumerable<IVideo> videos)
    {
        foreach (var video in videos)
            foreach (var tuple in GetListForVideo(video))
                yield return tuple;
    }

    public FileStreamResult GeneratePlaylist(
        IEnumerable<(IShokoEpisode ep, IReadOnlyList<IVideo> videos)> episodeList,
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
        foreach (var (episode, videos) in episodeList)
        {
            var series = episode.Series;
            if (series is null)
                continue;

            var index = 0;
            foreach (var video in videos)
                m3U8.Append(GetEpisodeEntry(new UriBuilder(uri.ToString()), series, episode, video, ++index, videos.Count));
        }

        var bytes = Encoding.UTF8.GetBytes(m3U8.ToString());
        var stream = new MemoryStream(bytes);
        return new FileStreamResult(stream, "application/x-mpegURL")
        {
            FileDownloadName = $"{name}.m3u8",
        };
    }

    private static string GetEpisodeEntry(UriBuilder uri, IShokoSeries series, IShokoEpisode episode, IVideo video, int part, int totalParts)
    {
        var poster = series.GetPreferredImageForType(ImageEntityType.Poster) ?? series.DefaultPoster;
        var parts = totalParts > 1 ? $" ({part}/{totalParts})" : string.Empty;
        var episodeNumber = episode.Type is EpisodeType.Episode
            ? episode.EpisodeNumber.ToString()
            : $"{episode.Type.ToString()[0]}{episode.EpisodeNumber}";
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
        queryString.Add("epCount", series.EpisodeCounts.Episodes.ToString());
        queryString.Add("restricted", series.Restricted ? "true" : "false");

        uri.Path = $"{(uri.Path.Length > 1 ? uri.Path + "/" : "/")}api/v3/File/{video.ID}/Stream";
        uri.Query = queryString.ToString();
        return $"#EXTINF:-1,{series.PreferredTitle} - {episodeNumber} - {episode.PreferredTitle}{parts}\n{uri}\n";
    }
}
