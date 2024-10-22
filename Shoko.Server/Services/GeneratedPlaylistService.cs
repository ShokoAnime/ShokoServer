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
        var episode = video.CrossReferences
            .Select(xref => xref.AnidbEpisode)
            .WhereNotNull()
            .OrderBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .FirstOrDefault();
        return GeneratePlaylistForEpisodeList(episode is not null ? [(episode, [video])] : []);
    }

    private FileStreamResult GeneratePlaylistForEpisodeList(
        IEnumerable<(IEpisode ep, IReadOnlyList<IVideo> videos)> episodeList,
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
            var anime = episode.Series;
            if (anime is null)
                continue;

            var index = 0;
            foreach (var video in videos)
                m3U8.Append(GetEpisodeEntry(new UriBuilder(uri.ToString()), anime, episode, video, ++index, videos.Count));
        }

        var bytes = Encoding.UTF8.GetBytes(m3U8.ToString());
        var stream = new MemoryStream(bytes);
        return new FileStreamResult(stream, "application/x-mpegURL")
        {
            FileDownloadName = $"{name}.m3u8",
        };
    }

    private static string GetEpisodeEntry(UriBuilder uri, ISeries anime, IEpisode episode, IVideo video, int part, int totalParts)
    {
        var poster = anime.GetPreferredImageForType(ImageEntityType.Poster) ?? anime.DefaultPoster;
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
        queryString.Add("animeId", anime.ID.ToString());
        queryString.Add("animeName", anime.PreferredTitle);
        queryString.Add("epId", episode.ID.ToString());
        queryString.Add("episodeName", episode.PreferredTitle);
        queryString.Add("epNo", episodeNumber);
        queryString.Add("epCount", anime.EpisodeCounts.Episodes.ToString());
        queryString.Add("restricted", anime.Restricted ? "true" : "false");

        uri.Path = $"{(uri.Path.Length > 1 ? uri.Path + "/" : "/")}api/v3/File/{video.ID}/Stream";
        uri.Query = queryString.ToString();
        return $"#EXTINF:-1,{episode.PreferredTitle} — {episodeNumber}{parts}\n{uri}\n";
    }
}
