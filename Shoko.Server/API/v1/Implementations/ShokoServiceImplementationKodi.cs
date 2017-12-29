using System;
using System.Net;
using Nancy.Rest.Module;
using NLog;
using Shoko.Models.Interfaces;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.PlexAndKodi.Kodi;
using Stream = System.IO.Stream;

namespace Shoko.Server.API.v1.Implementations
{
    public class ShokoServiceImplementationKodi : IShokoServerKodi
    {
        CommonImplementation _impl = new CommonImplementation();
        ShokoServiceImplementation service = new ShokoServiceImplementation();
        public static Logger logger = LogManager.GetCurrentClassLogger();


        public Stream GetSupportImage(string name)
        {
            return _impl.GetSupportImage(name);
        }

        public MediaContainer GetFilters(string userId)
        {
            return _impl.GetFilters(new KodiProvider {Nancy = RestModule.CurrentModule}, userId);
        }

        public MediaContainer GetMetadata(string userId, int type, string id, int? filterid)
        {
            return _impl.GetMetadata(new KodiProvider {Nancy = RestModule.CurrentModule}, userId, type, id, null, false,
                filterid);
        }

        public PlexContract_Users GetUsers()
        {
            return _impl.GetUsers(new KodiProvider {Nancy = RestModule.CurrentModule});
        }

        public Response Version()
        {
            return _impl.GetVersion();
        }

        public MediaContainer Search(string userId, int limit, string query)
        {
            return _impl.Search(new KodiProvider {Nancy = RestModule.CurrentModule}, userId, limit, query, false);
        }

        public MediaContainer SearchTag(string userId, int limit, string query)
        {
            return _impl.Search(new KodiProvider {Nancy = RestModule.CurrentModule}, userId, limit, query, true);
        }

        public Response ToggleWatchedStatusOnGroup(string userId, int groupid, bool status)
        {
            return _impl.ToggleWatchedStatusOnGroup(new KodiProvider {Nancy = RestModule.CurrentModule}, userId,
                groupid, status);
        }

        public Response ToggleWatchedStatusOnSeries(string userId, int serieid, bool status)
        {
            return _impl.ToggleWatchedStatusOnSeries(new KodiProvider {Nancy = RestModule.CurrentModule}, userId,
                serieid, status);
        }

        public Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status)
        {
            return _impl.ToggleWatchedStatusOnEpisode(new KodiProvider {Nancy = RestModule.CurrentModule}, userId, epid,
                status);
        }

        public Response Vote(string userId, int id, float votevalue, int votetype)
        {
            return _impl.VoteAnime(new KodiProvider {Nancy = RestModule.CurrentModule}, userId, id, votevalue,
                votetype);
        }

        public Response TraktScrobble(string userId, int type, float progress, int status)
        {
            return _impl.TraktScrobble(new KodiProvider {Nancy = RestModule.CurrentModule}, userId, type, progress,
                status);
        }

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