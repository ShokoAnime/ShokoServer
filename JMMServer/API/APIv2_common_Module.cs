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

            //Images
            Get["/get_image/{type}/{id}"] = parameter => { return GetImage(parameter.type, parameter.id); };
            Get["/get_image/{type}/{id}/{thumb}"] = parameter => { return GetImage(parameter.id, parameter.type, parameter.thumb); };
            Get["/get_thumb/{type}/{id}/{ratio}"] = parameter => { return GetThumb(parameter.type, parameter.id, parameter.ratio); };
            Get["/get_support_image/{name}"] = parameter => { return GetSupportImage(parameter.name); };
            Get["/get_support_image/{name}/{ratio}"] = parameter => { return GetSupportImage(parameter.name, parameter.ratio); };
            Get["/get_image_using_path/{path}"] = parameter => { return GetImageUsingPath(parameter.path); };            
            Get["/filters/get"] = _ => { return GetFilters(); };
            Get["/metadata/{type}/{id}"] = x => { return GetMetadata(x.type, x.id); };

            
            //Get["/Search/{uid}/{limit}/{query}"] = parameter => {  return Search_Kodi(parameter.uid, parameter.limit, parameter.query); };
            //Get["/Search/{uid}/{limit}/{query}/{searchTag}"] = parameter => { return SearchTag(parameter.uid, parameter.limit, parameter.query); };
            //Get["/ToggleWatchedStatusOnEpisode/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnEpisode_Kodi(parameter.uid, parameter.epid, parameter.status); };
            //Get["/VoteAnime/{uid}/{id}/{votevalue}/{votetype}"] = parameter => { return VoteAnime_Kodi(parameter.uid, parameter.id, parameter.votevalue, parameter.votetype); };
            //Get["/TraktScrobble/{animeid}/{type}/{progress}/{status}"] = parameter => { return TraktScrobble(parameter.animeid, parameter.type, parameter.progress, parameter.status); };
        }

        IProvider _prov_kodi = new KodiProvider();
        CommonImplementation _impl = new CommonImplementation();


        #region Images

        /// <summary>
        ///  Return image that is used as support image, images are build-in 
        /// </summary>
        /// <param name="name">name of image inside resource</param>
        /// <returns></returns>
        private object GetSupportImage(string name)
        {
            using (System.IO.Stream image = new CommonImplementation().GetSupportImage(name))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return image that is used as support image, images are build-in with given ratio
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetSupportImage(string name, string ratio)
        {
            using (System.IO.Stream image = new JMMServiceImplementationREST().GetSupportImage(name, ratio))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return image with given path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private object GetImageUsingPath(string path)
        {
            using (System.IO.Stream image = new JMMServiceImplementationREST().GetImageUsingPath(path))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return image with given type and id
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private object GetImage(string type, string id)
        {
            return GetImage(type, id, false);
        }

        /// <summary>
        /// Return image with given type, id and if this should be a thumbnail
        /// </summary>
        /// <param name="id"></param>
        /// <param name="type"></param>
        /// <param name="thumb"></param>
        /// <returns></returns>
        private object GetImage(string type, string id, bool thumb)
        {
            using (System.IO.Stream image = new JMMServiceImplementationREST().GetImage(type, id, thumb))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return thumbnail from given type and id with ratio
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetThumb(string type, string id, string ratio)
        {
            using (System.IO.Stream image = new JMMServiceImplementationREST().GetThumb(type, id, ratio))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        #endregion


        private object GetFilters()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                return _impl.GetFilters(_prov_kodi, user.JMMUserID.ToString());
            }
            else
            {
                return new APIMessage(500, "Internal server error");
            }
        }

        private object GetMetadata(string typeid, string id)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                return _impl.GetMetadata(_prov_kodi, user.JMMUserID.ToString(), typeid, id, null);
            }
            else
            {
                return new APIMessage(500, "Internal server error");
            }
        }


    }
}
