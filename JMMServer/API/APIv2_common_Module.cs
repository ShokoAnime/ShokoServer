using JMMServer.PlexAndKodi;
using JMMServer.PlexAndKodi.Kodi;
using Nancy;
using Nancy.Security;
using JMMServer.API.Model;

namespace JMMServer.API
{
    public class APIv2_common_Module : Nancy.NancyModule
    {
        public APIv2_common_Module() : base("/api")
        {
            this.RequiresAuthentication();
     
            Get["/filters/get"] = _ => { return GetFilters(); };
            Get["/metadata/{type}/{id}"] = x => { return GetMetadata(x.type, x.id); };
            Get["/metadata/{type}/{id}/nocast"] = x => { return GetMetadata(x.type, x.id, true); };
	        Get["/metadata/{type}/{id}/{filter}"] = x => { return GetMetadata(x.type, x.id, false, x.filter); };
	        Get["/metadata/{type}/{id}/nocast/{filter}"] = x => { return GetMetadata(x.type, x.id, true, x.filter); };


	        //Get["/Search/{uid}/{limit}/{query}"] = parameter => {  return Search_Kodi(parameter.uid, parameter.limit, parameter.query); };
            //Get["/Search/{uid}/{limit}/{query}/{searchTag}"] = parameter => { return SearchTag(parameter.uid, parameter.limit, parameter.query); };
            //Get["/ToggleWatchedStatusOnEpisode/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnEpisode_Kodi(parameter.uid, parameter.epid, parameter.status); };
            //Get["/VoteAnime/{uid}/{id}/{votevalue}/{votetype}"] = parameter => { return VoteAnime_Kodi(parameter.uid, parameter.id, parameter.votevalue, parameter.votetype); };
            //Get["/TraktScrobble/{animeid}/{type}/{progress}/{status}"] = parameter => { return TraktScrobble(parameter.animeid, parameter.type, parameter.progress, parameter.status); };
        }

        IProvider _prov_kodi = new KodiProvider();
        CommonImplementation _impl = new CommonImplementation();

        private object GetFilters()
        {
            API.APIv1_Legacy_Module.request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                return _impl.GetFilters(_prov_kodi, user.JMMUserID.ToString());
            }
            else
            {
                return new APIMessage(500, "Unable to get User");
            }
        }

        private object GetMetadata(string typeid, string id, bool nocast=false, string filter="")
        {
            API.APIv1_Legacy_Module.request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
	            int? filterid = filter.ParseNullableInt();
                return _impl.GetMetadata(_prov_kodi, user.JMMUserID.ToString(), typeid, id, null, nocast, filterid);
            }
            else
            {
                return new APIMessage(500, "Unable to get User");
            }
        }


    }
}
