using System;
using Nancy;
using System.Dynamic;
using System.Collections.Generic;
using JMMContracts.PlexAndKodi;

namespace JMMServer.API
{
    //class will be found automagicly thanks to inherits also class need to be public (or it will 404)
    public class API_Calls: Nancy.NancyModule
    {
        public API_Calls() : base("/api")
        {
            // CommonImplementation
            Get["/"] = parameter => { return IndexPage; };
            Get["/GetSupportImage/{name}"] = parameter => { return GetSupportImage(parameter.name); };
            Get["/GetFilters/{uid}"] = parameter => { return GetFilters(parameter.uid); };
            Get["/GetMetadata/{uid}/{type}/{id}/{historyinfo}"] = parameter => { return GetMetadata(parameter.uid, parameter.type, parameter.id, parameter.historyinfo); };
            Get["/GetUsers"] = parameter => { return GetUsers(); };
            Get["/GetVersion"] = parameter => { return GetVersion(); };
            Get["/Search/{uid}/{limit}/{query}/{searchTag}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, parameter.searchTag); };
            Get["/GetItemsFromGroup/{uid}/{gid}"] = parameter => { return GetItemsFromGroup(parameter.uid, parameter.gid); };
            Get["/ToggleWatchedStatusOnEpisode/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnEpisode(parameter.uid, parameter.epid, parameter.status); };
            Get["/VoteAnime/{uid}/{id}/{votevalue}/{votetype}"] = parameter => { return VoteAnime(parameter.uid, parameter.id, parameter.votevalue, parameter.votetype); };
            Get["/TraktScrobble/{animeid}/{type}/{progress}/{status}"] = parameter => { return TraktScrobble(parameter.animeid, parameter.type, parameter.progress, parameter.status); };
            Get["/GetItemsFromSerie/{uid}/{serieid}"] = paramter => { return GetItemsFromSerie(paramter.uid, paramter.serieid); };

            // KodiImplementation
            Get["/GetMetadata/{uid}/{type}/{id}"] = parameter => { return GetMetadata(parameter.uid, parameter.type, parameter.id, null); };
            Get["/Search/{uid}/{limit}/{query}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, false); };
            Get["/SearchTag/{uid}/{limit}/{query}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, true); };

            // PlexImplementation
            // nothing specific only provider

            // JMMServerRest
            Get["/GetImage/{type}/{id}"] = parameter => { return GetImage(parameter.type, parameter.id); };
            Get["/GetThumb/{type}/{id}/{ratio}"] = parameter => { return GetThumb(parameter.type, parameter.id, parameter.ratio); };
            Get["/GetSupportImage/{name}/{ratio}"] = parameter => { return GetSupportImage(parameter.name, parameter.ratio); };
            Get["/GetImageUsingPath/{path}"] = parameter => { return GetImageUsingPath(parameter.path); };

            // IJMMServerKodi
            Get["/GetMetadata/{uid}/{type}/{id}"] = parameter => { return GetMetadata(parameter.uid, parameter.type, parameter.id, null); };
        }

        const String IndexPage = @"<html><body><h1>JMMServer is running</h1></body></html>";

        //TODO APIv2: should _prov be path/road for different function to trigger kodi/plex
        PlexAndKodi.IProvider _prov = new PlexAndKodi.Kodi.KodiProvider();
        PlexAndKodi.CommonImplementation _impl = new PlexAndKodi.CommonImplementation();
        JMMServiceImplementationREST _rest = new JMMServiceImplementationREST();
        MediaContainer api_media;
        System.IO.Stream image;
        JMMContracts.PlexAndKodi.Response respond;
        Nancy.Response response;
        dynamic moe = new ExpandoObject();
        //TODO APIv2: do we need BreadCrumbs ?
        PlexAndKodi.BreadCrumbs info;

        /// <summary>
        /// List all Group/Filters for given user ID
        /// </summary>
        private object GetFilters(int uid)
        {
            api_media = _impl.GetFilters(_prov, uid.ToString());
            JMMContracts.API.Models.Filters Filters = new JMMContracts.API.Models.Filters(api_media);
            return Filters;
        }

        /// <summary>
        /// Return image that is used as support image, images are build-in 
        /// </summary>
        private object GetSupportImage(string name)
        {
            image = _impl.GetSupportImage(name);
            response = new Nancy.Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

        /// <summary>
        /// Return MetaData about episode, series, files
        /// </summary>
        private object GetMetadata(string uid, string typeid, string id, string historyinfo)
        {
            api_media = _impl.GetMetadata(_prov, uid, typeid, id, historyinfo);
            JMMContracts.API.Models.Metadatas metadatas = new JMMContracts.API.Models.Metadatas(typeid, api_media);
            return metadatas;
        }

        /// <summary>
        /// Return Users with ErrorString and List os users inside System
        /// </summary>
        private object GetUsers()
        {
            JMMContracts.PlexAndKodi.PlexContract_Users plexUsers =_impl.GetUsers(_prov);
            JMMContracts.API.Models.Users Users = new JMMContracts.API.Models.Users(plexUsers);
            return Users;
        }

        /// <summary>
        /// Return current version of JMMServer
        /// </summary>
        private object GetVersion()
        {
            JMMContracts.API.Models.Version version = new JMMContracts.API.Models.Version();
            return version;
        }

        /// <summary>
        /// Return Series that match serched quote
        /// </summary>
        private object Search(string uid, string limit, string query, bool searchTag)
        {
            api_media = _impl.Search(_prov, uid, limit, query, searchTag);
            JMMContracts.API.Models.Series series = new JMMContracts.API.Models.Series(api_media);
            return series;
        }

        //TODO APIv2: once uid is string once its int
        /// <summary>
        /// return series...
        /// </summary>
        private object GetItemsFromGroup(int uid, string gid)
        {
            info = new PlexAndKodi.BreadCrumbs();
            api_media = _impl.GetItemsFromGroup(_prov, uid, gid, info);
            JMMContracts.API.Models.Series series = new JMMContracts.API.Models.Series(api_media);
            return series;
        }

        /// <summary>
        /// Set watch status for given episode id
        /// </summary>
        private object ToggleWatchedStatusOnEpisode(string uid, string epid, string status)
        {
            respond = _impl.ToggleWatchedStatusOnEpisode(_prov, uid, epid, status);
            return response;
        }

        /// <summary>
        /// Rate episode/movie
        /// </summary>
        private object VoteAnime(string uid, string id, string votevalue, string votetype)
        {
            respond = _impl.VoteAnime(_prov, uid, id, votevalue, votetype);
            return response;
        }

        /// <summary>
        /// Set current Scrobbled series/movie 
        /// </summary>
        private object TraktScrobble(string animeid, string type, string progress, string status)
        {
            respond = _impl.TraktScrobble(_prov, animeid, type, progress, status);
            return response;
        }

        /// <summary>
        /// Return series/season from inside other season ?(like when serie have ova/episodes/credits?)
        /// </summary>
        private object GetItemsFromSerie(int uid, string serieid)
        {
            info = new PlexAndKodi.BreadCrumbs();
            api_media = _impl.GetItemsFromSerie(_prov, uid, serieid, info);
            JMMContracts.API.Models.Series series = new JMMContracts.API.Models.Series(api_media);
            return series;
        }

        //REST
        
         /// <summary>
         /// Return image
         /// </summary>
         /// <param name="type">type of image</param>
         /// <param name="id">image id</param>
         /// <returns></returns>
        private object GetImage(string type, string id)
        {
            image = _rest.GetImage(type, id);
            response = new Nancy.Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }
        
        /// <summary>
        /// Return thumbnail with given ratio
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetThumb(string type, string id, string ratio)
        {
            image = _rest.GetThumb(type, id, ratio);
            response = new Nancy.Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

        /// <summary>
        /// Return image that is used as support image, images are build-in with given ratio
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetSupportImage(string name, string ratio)
        {
            image = _rest.GetSupportImage(name, ratio);
            response = new Nancy.Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

        /// <summary>
        /// Return image with given path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private object GetImageUsingPath(string path)
        {
            image = _rest.GetImageUsingPath(path);
            response = new Nancy.Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }
    }
}
