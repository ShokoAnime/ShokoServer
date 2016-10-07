﻿using System;
using System.IO;
using Nancy;
using JMMServer.PlexAndKodi;
using JMMServer.PlexAndKodi.Plex;
using JMMServer.PlexAndKodi.Kodi;
using JMMContracts.PlexAndKodi;
using Nancy.Security;
using JMMServer.API;
using JMMServer.API.Model;
using Stream = System.IO.Stream;

namespace JMMServer.API
{
    //Legacy module, unitil all client are moved to APIv2 this need to stay
    public class APIv1_Legacy_Module : Nancy.NancyModule
    {
        public APIv1_Legacy_Module() : base("/")
        {
            //this.RequiresAuthentication();

            // KodiImplementation
            Get["/JMMServerKodi/GetSupportImage/{name}"] = parameter => { return GetSupportImageRest(parameter.name); };
            Get["/JMMServerKodi/GetFilters/{uid}"] = parameter => { return GetFilters_Kodi(parameter.uid); };
            Get["/JMMServerKodi/GetMetadata/{uid}/{type}/{id}"] = parameter => { return GetMetadata_Kodi(parameter.uid, parameter.type, parameter.id); };
            Get["/JMMServerKodi/GetMetadata/{uid}/{type}/{id}/nocast"] = parameter => { return GetMetadata_Kodi(parameter.uid, parameter.type, parameter.id, true); };
            Get["/JMMServerKodi/GetUsers"] = _ => { return GetUsers_Kodi(); };
            Get["/JMMServerKodi/GetVersion"] = _ => { return GetVersion(); };
            Get["/JMMServerKodi/Search/{uid}/{limit}/{query}"] = parameter => { return Search_Kodi(parameter.uid, parameter.limit, parameter.query); };
            Get["/JMMServerKodi/SearchTag/{uid}/{limit}/{query}"] = parameter => { return SearchTag(parameter.uid, parameter.limit, parameter.query); };
            Get["/JMMServerKodi/Watch/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnEpisode_Kodi(parameter.uid, parameter.epid, parameter.status); };
			Get["/JMMServerKodi/WatchSeries/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnSeries_Kodi(parameter.uid, parameter.epid, parameter.status); };
			Get["/JMMServerKodi/WatchGroup/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnGroup_Kodi(parameter.uid, parameter.epid, parameter.status); };
			Get["/JMMServerKodi/Vote/{uid}/{id}/{votevalue}/{votetype}"] = parameter => { return VoteAnime_Kodi(parameter.uid, parameter.id, parameter.votevalue, parameter.votetype); };
            Get["/JMMServerKodi/TraktScrobble/{animeid}/{type}/{progress}/{status}"] = parameter => { return TraktScrobble(parameter.animeid, parameter.type, parameter.progress, parameter.status); };

            // PlexImplementation
            Get["/JMMServerPlex/GetSupportImage/{name}"] = parameter => { return GetSupportImageRest(parameter.name); };
            Get["/JMMServerPlex/GetFilters/{uid}"] = parameter => { return GetFilters_Plex(parameter.uid); };
            Get["/JMMServerPlex/GetMetadata/{uid}/{type}/{id}/{historyinfo}"] = parameter => { return GetMetadata_Plex(parameter.uid, parameter.type, parameter.id, parameter.historyinfo); };
            Get["/JMMServerPlex/GetUsers"] = _ => { return GetUsers_Plex(); };
            Get["/JMMServerPlex/Search/{uid}/{limit}/{query}"] = parameter => { return Search_Plex(parameter.uid, parameter.limit, parameter.query); };
            Get["/JMMServerPlex/Watch/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnEpisode_Plex(parameter.uid, parameter.epid, parameter.status); };
            Get["/JMMServerPlex/Vote/{uid}/{id}/{votevalue}/{votetype}"] = parameter => { return VoteAnime_Plex(parameter.uid, parameter.id, parameter.votevalue, parameter.votetype); };

            // JMMServerRest
            Get["/JMMServerREST/GetImage/{type}/{id}"] = parameter => { return GetImageRest(parameter.type, parameter.id); };
            Get["/JMMServerREST/GetThumb/{type}/{id}/{ratio}"] = parameter => { return GetThumbRest(parameter.type, parameter.id, parameter.ratio); };
            Get["/JMMServerREST/GetSupportImage/{name}/{ratio}"] = parameter => { return GetSupportImageRest(parameter.name, parameter.ratio); };
            Get["/JMMServerREST/GetImageUsingPath/{path}"] = parameter => { return GetImageUsingPathRest(parameter.path); };

            // JMMServerImage
            Get["/JMMServerImage/GetImage/{id}/{type}/{thumb}"] = parameter => { return GetImage(parameter.id, parameter.type, parameter.thumb); };
			Get["/JMMServerImage/GetImageUsingPath/{path}"] = parameter => { return GetImageUsingPath(parameter.path); };
        }


        CommonImplementation _impl = new CommonImplementation();
        IProvider _prov_kodi = new KodiProvider();
        IProvider _prov_plex = new PlexProvider();
        JMMServiceImplementationREST _rest = new JMMServiceImplementationREST();
	    JMMServiceImplementationImage _image = new JMMServiceImplementationImage();
	    Nancy.Response response;
        public static Nancy.Request request;

        //Common

        /// <summary>
        ///  Return image that is used as support image, images are build-in 
        /// </summary>
        /// <param name="name">name of image inside resource</param>
        /// <returns></returns>
        private object GetSupportImageRest(string name)
        {
            using (System.IO.Stream image = _impl.GetSupportImage(name))
            {
                response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        //KODI

        /// <summary>
        /// KODI: List all Group/Filters for given user ID
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <returns></returns>
        private object GetFilters_Kodi(int uid)
        {
            return _impl.GetFilters(_prov_kodi, uid.ToString());
        }

        /// <summary>
        /// KODI: Return MetaData about episode, series, files
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <param name="typeid">Type ID</param>
        /// <param name="id">Object ID</param>
        /// <param name="historyinfo">BreadCrumbs string</param>
        /// <returns></returns>
        private object GetMetadata_Kodi(string uid, string typeid, string id, bool nocast=false)
        {
            return _impl.GetMetadata(_prov_kodi, uid, typeid, id, null, nocast);
        }

        /// <summary>
        /// Return current version of JMMServer
        /// </summary>
        /// <returns></returns>
        private object GetVersion()
        {
            return _impl.GetVersion();
        }

        /// <summary>
        /// KODI: Return Users with ErrorString and List os users inside System
        /// </summary>
        /// <returns></returns>
        private PlexContract_Users GetUsers_Kodi()
        {
            return _impl.GetUsers(_prov_kodi);
        }

        /// <summary>
        /// KODI: Return Series that match searched quote
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <param name="limit">Max count of result</param>
        /// <param name="query">Query</param>
        /// <param name="searchTag">Searching for Tag?</param>
        /// <returns></returns>
        private object Search_Kodi(string uid, string limit, string query)
        {
            return _impl.Search(_prov_kodi, uid, limit, query, false);
        }

        /// <summary>
        /// KODI: Return Series that match tag with searched quote
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <param name="limit">Max count of result</param>
        /// <param name="query">Query</param>
        /// <param name="searchTag">Searching for Tag?</param>
        /// <returns></returns>
        private object SearchTag(string uid, string limit, string query)
        {
            return _impl.Search(_prov_kodi, uid, limit, query, true);
        }

        /// <summary>
        /// KODI: Set watch status for given episode id
        /// </summary>
        /// <param name="userid">User ID</param>
        /// <param name="episodeid">Episode ID (JMM ID)</param>
        /// <param name="watchedstatus">Watched status 1:true 0:false</param>
        /// <returns></returns>
        private object ToggleWatchedStatusOnEpisode_Kodi(string userid, string episodeid, string watchedstatus)
        {
            return _impl.ToggleWatchedStatusOnEpisode(_prov_kodi, userid, episodeid, watchedstatus);
        }

		/// <summary>
		/// KODI: Set watch status for given series id
		/// </summary>
		/// <param name="userid">User ID</param>
		/// <param name="seriesid">Series ID (JMM ID)</param>
		/// <param name="watchedstatus">Watched status 1:true 0:false</param>
		/// <returns></returns>
		private object ToggleWatchedStatusOnSeries_Kodi(string userid, string seriesid, string watchedstatus)
		{
			return _impl.ToggleWatchedStatusOnSeries(_prov_kodi, userid, seriesid, watchedstatus);
		}

		/// <summary>
		/// KODI: Set watch status for given group id
		/// </summary>
		/// <param name="userid">User ID</param>
		/// <param name="groupid">Group ID (JMM ID)</param>
		/// <param name="watchedstatus">Watched status 1:true 0:false</param>
		/// <returns></returns>
		private object ToggleWatchedStatusOnGroup_Kodi(string userid, string groupid, string watchedstatus)
		{
			return _impl.ToggleWatchedStatusOnGroup(_prov_kodi, userid, groupid, watchedstatus);
		}

		/// <summary>
		/// KODI: Rate episode/movie
		/// </summary>
		/// <param name="uid">User ID</param>
		/// <param name="id">Object ID</param>
		/// <param name="votevalue">Rating</param>
		/// <param name="votetype">Vote type: Anime = 1, AnimeTemp = 2, Group = 3, Episode = 4</param>
		/// <returns></returns>
		private object VoteAnime_Kodi(string uid, string id, string votevalue, string votetype)
        {
            return _impl.VoteAnime(_prov_kodi, uid, id, votevalue, votetype);
        }

        /// <summary>
        /// KODI: Set current Scrobbled series/movie 
        /// </summary>
        /// <param name="animeid">Anime ID</param>
        /// <param name="type">Type (series/movie)</param>
        /// <param name="progress"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        private object TraktScrobble(string animeid, string type, string progress, string status)
        {
            return _impl.TraktScrobble(_prov_kodi, animeid, type, progress, status);
        }

        //PLEX

        /// <summary>
        /// Plex: List all Group/Filters for given user ID
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <returns></returns>
        private object GetFilters_Plex(string uid)
        {
            return _impl.GetFilters(_prov_plex, uid);
        }

        /// <summary>
        /// Plex: Return MetaData about episode, series, files
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <param name="typeid">Type ID</param>
        /// <param name="id">Object ID</param>
        /// <param name="historyinfo">BreadCrumbs string</param>
        /// <returns></returns>
        private object GetMetadata_Plex(string uid, string typeid, string id, string historyinfo)
        {
            return _impl.GetMetadata(_prov_plex, uid, typeid, id, historyinfo);
        }

        /// <summary>
        /// Plex: Return Users with ErrorString and List os users inside System
        /// </summary>
        /// <returns></returns>
        private PlexContract_Users GetUsers_Plex()
        {
            return _impl.GetUsers(_prov_plex);
        }

        /// <summary>
        /// Plex: Return Series that match searched quote
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <param name="limit">Max count of result</param>
        /// <param name="query">Query</param>
        /// <param name="searchTag">Searching for Tag?</param>
        /// <returns></returns>
        private object Search_Plex(string uid, string limit, string query)
        {
            return _impl.Search(_prov_plex, uid, limit, query, false);
        }

        /// <summary>
        /// Plex: Set watch status for given episode id
        /// </summary>
        /// <param name="userid">User ID</param>
        /// <param name="episodeid">Episode ID (JMM ID)</param>
        /// <param name="watchedstatu">Watched status 1:true 0:false</param>
        /// <returns></returns>
        private object ToggleWatchedStatusOnEpisode_Plex(string userid, string episodeid, string watchedstatus)
        {
            return _impl.ToggleWatchedStatusOnEpisode(_prov_plex, userid, episodeid, watchedstatus);
        }

        /// <summary>
        /// Plex:Rate episode/movie
        /// </summary>
        /// <param name="uid">User ID</param>
        /// <param name="id">Object ID</param>
        /// <param name="votevalue">Rating</param>
        /// <param name="votetype">Vote type: Anime = 1, AnimeTemp = 2, Group = 3, Episode = 4</param>
        /// <returns></returns>
        private object VoteAnime_Plex(string uid, string id, string votevalue, string votetype)
        {
            return _impl.VoteAnime(_prov_plex, uid, id, votevalue, votetype);
        }

        //REST

        /// <summary>
        /// Return image
        /// </summary>
        /// <param name="type">image type</param>
        /// <param name="id">image id</param>
        /// <returns></returns>
        private object GetImageRest(string type, string id)
        {
            response = new Nancy.Response();
            string content;
            Stream image = _rest.GetImage(type, id, false, out content);
            response = new Nancy.Response();
            response = Response.FromStream(image, content);
            return response;
        }

        /// <summary>
        /// Return thumbnail with given ratio
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetThumbRest(string type, string id, string ratio)
        {
	        string contentType;
            System.IO.Stream image = _rest.GetThumb(type, id, ratio, out contentType);
            response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }

        /// <summary>
        /// Return image that is used as support image, images are build-in with given ratio
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetSupportImageRest(string name, string ratio)
        {
            System.IO.Stream image = _rest.GetSupportImage(name, ratio);
            response = new Nancy.Response();
	        // This will always be png, so we are ok
            response = Response.FromStream(image, "image/png");
            return response;
        }

        /// <summary>
        /// Return image with given path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private object GetImageUsingPathRest(string path)
        {
            System.IO.Stream image = _rest.GetImageUsingPath(path);
            response = new Nancy.Response();
            response = Response.FromStream(image, MimeTypes.GetMimeType(path));
            return response;
        }

        /// <summary>
        /// Return image with given Id type and information if its should be thumb
        /// </summary>
        /// <param name="id"></param>
        /// <param name="type"></param>
        /// <param name="thumb"></param>
        /// <returns></returns>
        private object GetImageRest(string id, string type, bool thumb)
        {
	        string contentType;
            System.IO.Stream image = _rest.GetImage(type, id, thumb, out contentType);
            response = new Nancy.Response();
	        // This is not always png
            response = Response.FromStream(image, contentType);
            return response;
        }

	    /// <summary>
	    /// Return image with given Id type and information if its should be thumb
	    /// </summary>
	    /// <param name="id"></param>
	    /// <param name="type"></param>
	    /// <param name="thumb"></param>
	    /// <returns></returns>
	    private object GetImage(string id, string type, bool thumb)
	    {
		    int imgtype = int.Parse(type);
			string contentType;
		    byte[] image = _image.GetImage(id, imgtype, thumb, out contentType);
			if(image == null || contentType == "")
			{
				return new APIMessage(500, "Image of type not found for ID");
			}
			response = Response.FromByteArray(image, contentType);
		    return response;
	    }

		/// <summary>
		/// Return image with given path
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		private object GetImageUsingPath(string path)
		{
			string contentType = MimeTypes.GetMimeType(path);
			byte[] image = _image.GetImageUsingPath(path);
			response = Response.FromByteArray(image, contentType);
			return response;
		}
	}
}
