using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Models.Interfaces;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.Commands;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.PlexAndKodi.Kodi;
using Shoko.Server.Settings;
using Stream = System.IO.Stream;

namespace Shoko.Server.API.v1.Implementations;

[ApiController]
[Route("/api/Kodi")]
[ApiVersion("1.0", Deprecated = true)]
public class ShokoServiceImplementationKodi : IShokoServerKodi, IHttpContextAccessor
{
    private readonly ShokoServiceImplementation _service;
    public HttpContext HttpContext { get; set; }

    private readonly CommonImplementation _impl;

    private readonly ILogger<ShokoServiceImplementationKodi> _logger;
    private readonly ISettingsProvider _settingsProvider;

    public ShokoServiceImplementationKodi(ICommandRequestFactory commandFactory,
        ILogger<ShokoServiceImplementationKodi> logger, ISettingsProvider settingsProvider, CommonImplementation impl)
    {
        _settingsProvider = settingsProvider;
        _service = new ShokoServiceImplementation(null, null, null, commandFactory, _settingsProvider);
        _logger = logger;
        _impl = impl;
    }


    [HttpGet("Image/Support/{name}")]
    public Stream GetSupportImage(string name)
    {
        return _impl.GetSupportImage(name);
    }

    [HttpGet("Filters/{userId}")]
    public MediaContainer GetFilters(string userId)
    {
        return _impl.GetFilters(new KodiProvider { HttpContext = HttpContext }, userId);
    }

    [HttpGet("Metadata/{userId}/{type}/{id}/{filterid?}")]
    public MediaContainer GetMetadata(string userId, int type, string id, int? filterid)
    {
        return _impl.GetMetadata(new KodiProvider { HttpContext = HttpContext }, userId, type, id, null, false,
            filterid);
    }

    [HttpGet("User")]
    public PlexContract_Users GetUsers()
    {
        return _impl.GetUsers(new KodiProvider { HttpContext = HttpContext });
    }

    [HttpGet("Version")]
    public Response Version()
    {
        return _impl.GetVersion();
    }

    [HttpGet("Search/{userId}/{limit}/{query}")]
    public MediaContainer Search(string userId, int limit, string query)
    {
        return _impl.Search(new KodiProvider { HttpContext = HttpContext }, userId, limit, query, false);
    }

    [HttpGet("SearchTag/{userId}/{limit}/{query}")]
    public MediaContainer SearchTag(string userId, int limit, string query)
    {
        return _impl.Search(new KodiProvider { HttpContext = HttpContext }, userId, limit, query, true);
    }

    [HttpGet("Group/Watch/{userId}/{groupid}/{status}")]
    public Response ToggleWatchedStatusOnGroup(string userId, int groupid, bool status)
    {
        return _impl.ToggleWatchedStatusOnGroup(new KodiProvider { HttpContext = HttpContext }, userId,
            groupid, status);
    }

    [HttpGet("Serie/Watch/{userId}/{serieid}/{status}")]
    public Response ToggleWatchedStatusOnSeries(string userId, int serieid, bool status)
    {
        return _impl.ToggleWatchedStatusOnSeries(new KodiProvider { HttpContext = HttpContext }, userId,
            serieid, status);
    }

    [HttpGet("Serie/Watch/{userId}/{epid}/{status}")]
    public Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status)
    {
        return _impl.ToggleWatchedStatusOnEpisode(new KodiProvider { HttpContext = HttpContext }, userId, epid,
            status);
    }

    [HttpGet("Vote/{userId}/{id}/{votevalue}/{votetype}")]
    public Response Vote(string userId, int id, float votevalue, int votetype)
    {
        return _impl.VoteAnime(new KodiProvider { HttpContext = HttpContext }, userId, id, votevalue,
            votetype);
    }

    [HttpGet("Trakt/Scrobble/{animeId}/{type}/{progress}/{status}")]
    public Response TraktScrobble(string animeId, int type, float progress, int status)
    {
        return _impl.TraktScrobble(new KodiProvider { HttpContext = HttpContext }, animeId, type, progress,
            status);
    }

    [HttpGet("Video/Rescan/{vlid}")]
    public Response Rescan(int vlid)
    {
        var r = new Response();
        try
        {
            var output = _service.RescanFile(vlid);
            if (!string.IsNullOrEmpty(output))
            {
                r.Code = HttpStatusCode.BadRequest.ToString();
                r.Message = output;
                return r;
            }

            r.Code = HttpStatusCode.OK.ToString();
        }
        catch (Exception ex)
        {
            r.Code = "500";
            r.Message = "Internal Error : " + ex;
            _logger.LogError(ex, "{Ex}", ex.ToString());
        }

        return r;
    }


    [HttpGet("Video/Rehash/{vlid}")]
    public Response Rehash(int vlid)
    {
        var r = new Response();
        try
        {
            _service.RehashFile(vlid);
            r.Code = HttpStatusCode.OK.ToString();
        }
        catch (Exception ex)
        {
            r.Code = "500";
            r.Message = "Internal Error : " + ex;
            _logger.LogError(ex, "{Ex}", ex.ToString());
        }

        return r;
    }
}
