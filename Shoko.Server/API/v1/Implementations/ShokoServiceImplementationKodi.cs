using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Models.Interfaces;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.PlexAndKodi.Kodi;
using Stream = System.IO.Stream;

namespace Shoko.Server.API.v1.Implementations
{
    [ApiController]
    [Route("/api/Kodi")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ShokoServiceImplementationKodi : IShokoServerKodi, IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }

        CommonImplementation _impl = new CommonImplementation();
        ShokoServiceImplementation service = new ShokoServiceImplementation();
        public static Logger logger = LogManager.GetCurrentClassLogger();


        [HttpGet("Image/Support/{name}")]
        public Stream GetSupportImage(string name)
        {
            return _impl.GetSupportImage(name);
        }

        [HttpGet("Filters/{userId}")]
        public MediaContainer GetFilters(string userId)
        {
            return _impl.GetFilters(new KodiProvider {Nancy = HttpContext}, userId);
        }

        [HttpGet("Metadata/{userId}/{type}/{id}/{filterid?}")]
        public MediaContainer GetMetadata(string userId, int type, string id, int? filterid)
        {
            return _impl.GetMetadata(new KodiProvider {Nancy = HttpContext}, userId, type, id, null, false,
                filterid);
        }

        [HttpGet("User")]
        public PlexContract_Users GetUsers()
        {
            return _impl.GetUsers(new KodiProvider {Nancy = HttpContext});
        }

        [HttpGet("Version")]
        public Response Version()
        {
            return _impl.GetVersion();
        }

        [HttpGet("Search/{userId}/{limit}/{query}")]
        public MediaContainer Search(string userId, int limit, string query)
        {
            return _impl.Search(new KodiProvider {Nancy = HttpContext}, userId, limit, query, false);
        }

        [HttpGet("SearchTag/{userId}/{limit}/{query}")]
        public MediaContainer SearchTag(string userId, int limit, string query)
        {
            return _impl.Search(new KodiProvider {Nancy = HttpContext}, userId, limit, query, true);
        }

        [HttpGet("Group/Watch/{userId}/{groupid}/{status}")]
        public Response ToggleWatchedStatusOnGroup(string userId, int groupid, bool status)
        {
            return _impl.ToggleWatchedStatusOnGroup(new KodiProvider {Nancy = HttpContext}, userId,
                groupid, status);
        }

        [HttpGet("Serie/Watch/{userId}/{serieid}/{status}")]
        public Response ToggleWatchedStatusOnSeries(string userId, int serieid, bool status)
        {
            return _impl.ToggleWatchedStatusOnSeries(new KodiProvider {Nancy = HttpContext}, userId,
                serieid, status);
        }

        [HttpGet("Serie/Watch/{userId}/{epid}/{status}")]
        public Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status)
        {
            return _impl.ToggleWatchedStatusOnEpisode(new KodiProvider {Nancy = HttpContext}, userId, epid,
                status);
        }

        [HttpGet("Vote/{userId}/{id}/{votevalue}/{votetype}")]
        public Response Vote(string userId, int id, float votevalue, int votetype)
        {
            return _impl.VoteAnime(new KodiProvider {Nancy = HttpContext}, userId, id, votevalue,
                votetype);
        }

        [HttpGet("Trakt/Scrobble/{animeid}/{type}/{progress}/{status}")]
        public Response TraktScrobble(string userId, int type, float progress, int status)
        {
            return _impl.TraktScrobble(new KodiProvider {Nancy = HttpContext}, userId, type, progress,
                status);
        }

        [HttpGet("Video/Rescan/{vlid}")]
        public Response Rescan(int vlid)
        {
            Response r = new Response();
            try
            {
                string output = service.RescanFile(vlid);
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
                logger.Error(ex, ex.ToString());
            }
            return r;
        }


        [HttpGet("Video/Rehash/{vlid}")]
        public Response Rehash(int vlid)
        {
            Response r = new Response();
            try
            {
                service.RehashFile(vlid);
                r.Code = HttpStatusCode.OK.ToString();
            }
            catch (Exception ex)
            {
                r.Code = "500";
                r.Message = "Internal Error : " + ex;
                logger.Error(ex, ex.ToString());
            }
            return r;
        }
    }
}