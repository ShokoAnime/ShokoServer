using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class GeneratedPlaylistService
{
    private readonly HttpContext _context;

    public GeneratedPlaylistService(IHttpContextAccessor contentAccessor)
    {
        _context = contentAccessor.HttpContext!;
    }

    public FileStreamResult GeneratePlaylistForVideo(IVideo video)
    {
        var episode = video.Episodes
            .OrderBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .FirstOrDefault();
        return GeneratePlaylistForEpisodeList(episode is not null ? [(episode, [video])] : []);
    }

    private FileStreamResult GeneratePlaylistForEpisodeList(
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
