using System;
using Nancy;
using System.Dynamic;
using System.Collections.Generic;
using JMMContracts.API;

namespace JMMServer.API
{
    //class will be found automagicly thanks to inherits also class need to be public (error404)
    public class API_Calls: Nancy.NancyModule
    {
        public API_Calls()
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
            Get["/VoteAnime/{uid}/{objid}/{votevalue}/{votetype}"] = parameter => { return VoteAnime(parameter.uid, parameter.objid, parameter.votevalue, parameter.votetype); };
            Get["/TraktScrobble/{animeid}/{type}/{progress}/{status}"] = parameter => { return TraktScrobble(parameter.animeid, parameter.type, parameter.progress, parameter.status); };
            Get["/GetItemsFromSerie/{uid}/{serieid}"] = paramter => { return GetItemsFromSerie(paramter.uid, paramter.serieid); };

            // KodiImplementation
            Get["/GetMetadata/{uid}/{type}/{id}"] = parameter => { return GetMetadata(parameter.uid, parameter.type, parameter.id, null); };
            Get["/Search/{uid}/{limit}/{query}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, false); };
            Get["/SearchTag/{uid}/{limit}/{query}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, true); };

            // PlexImplementation
            // nothing specific only provider
        }

        const String IndexPage = @"<html><body><h1>JMMServer is running</h1></body></html>";

        //TODO API: _prov should change probably on path/road for different function to trigger kodi/plex
        PlexAndKodi.IProvider _prov = new PlexAndKodi.Kodi.KodiProvider();
        PlexAndKodi.CommonImplementation _impl = new PlexAndKodi.CommonImplementation();
        JMMContracts.PlexAndKodi.MediaContainer media;
        JMMContracts.API.API_MediaContainer api_media;
        System.IO.Stream image;
        JMMContracts.PlexAndKodi.Response respond;
        Response response;
        dynamic moe = new ExpandoObject();
        //TODO API: do we need BreadCrumbs ?
        PlexAndKodi.BreadCrumbs info;

        private object GetFilters(int uid)
        {
            media = _impl.GetFilters(_prov, uid.ToString());
            api_media = (JMMContracts.API.API_MediaContainer)media;

            dynamic MediaCont = new ExpandoObject();
            List<ExpandoObject> Medias = new List<ExpandoObject>();

            foreach (API_Video vid in api_media.Childrens)
            {
                moe = new ExpandoObject();
                moe.id = vid.Id;
                moe.title = vid.Title;
                moe.key = vid.Key;
                moe.count = vid.LeafCount;
                dynamic art = new ExpandoObject();
                if (vid.Thumb != null)
                {
                    art.thumb = vid.Thumb;
                }
                if (vid.Art != null)
                {
                    art.fanart = vid.Art;
                }
                //TODO API: Posters
                moe.art = art;
                Medias.Add(moe);
            }

            MediaCont.count = api_media.TotalSize;
            MediaCont.groups = Medias;
            
            



            response = new Response();
            //response = Response.AsJson<JMMContracts.API.API_MediaContainer>(api_media);
            response = Response.AsJson((ExpandoObject)MediaCont);
            response.ContentType = "application/json";
            return response;
        }

        private object GetSupportImage(string name)
        {
            image = _impl.GetSupportImage(name);
            response = new Response();
            response = Response.FromStream(image, "image/jpeg");
            return response;
        }
        
        private object GetMetadata(string uid, string typeid, string id, string historyinfo)
        {
            media = _impl.GetMetadata(_prov, uid, typeid, id, historyinfo);
            api_media = (JMMContracts.API.API_MediaContainer)media;

            dynamic Series = new ExpandoObject();
            List<ExpandoObject> series = new List<ExpandoObject>();
            foreach (API_Video vid in api_media.Childrens)
            {
                moe = new ExpandoObject();
                moe.id = vid.Id;
                moe.count_local = vid.ChildCount;
                dynamic tags = new List<ExpandoObject>();
                if (vid.Tags != null)
                {
                    foreach (API_Tag tag in vid.Tags)
                    {
                        tags.Add(tag);
                    }
                }
                moe.tag = tags;

                dynamic Role = new List<ExpandoObject>();
                dynamic moe_tag = new ExpandoObject();
                if (vid.Roles != null)
                {
                    foreach (API_RoleTag role in vid.Roles)
                    {
                        moe_tag.actor = role.Value;
                        moe_tag.actorpic = role.TagPicture;
                        moe_tag.role = role.Role;
                        moe_tag.roledesc = role.RoleDescription;
                        moe_tag.rolepic = role.RolePicture;
                        Role.Add(moe_tag);                    
                    }
                }
                moe.cast = Role;
                moe.title = vid.Title;
                moe.year = vid.Year;
                moe.episode = vid.LeafCount;
                moe.season = vid.Season;
                moe.rating = vid.Rating;
                //votes
                //my_rating
                moe.datastart = vid.AirDate;
                //datastop
                moe.plot = vid.Summary;
                //moe.status = vid
                moe.dateadded = vid.AddedAt;
                moe.key = vid.Key;

                dynamic art = new ExpandoObject();
                if (vid.Thumb != null)
                {
                    art.thumb = vid.Thumb;
                }
                if (vid.Art != null)
                {
                    art.fanart = vid.Art;
                }
                //poster
                moe.art = art;
                //source?
                moe.source = vid.SourceTitle;
                moe.watched = vid.ViewedLeafCount;
                dynamic Genre = new List<string>();
                if (vid.Genres != null)
                {
                    foreach (API_Tag genre in vid.Genres)
                    {
                        Genre.Add(genre.Value);
                    }
                }
                moe.genre = Genre;

                series.Add(moe);
            }


            Series.count = media.LeafCount;
            Series.series = series;

            response = new Response();
            //response = Response.AsJson<JMMContracts.API.API_MediaContainer>(api_media);
            response = Response.AsJson((ExpandoObject)Series);
            response.ContentType = "application/json";
            return response;
        }

        private object GetUsers()
        {
            JMMContracts.PlexAndKodi.PlexContract_Users plexUsers =_impl.GetUsers(_prov);
            List<ExpandoObject> Users = new List<ExpandoObject>();
            foreach (JMMContracts.PlexAndKodi.PlexContract_User user in plexUsers.Users)
            {
                moe = new ExpandoObject();
                moe.id = user.id;
                moe.name = user.name;
                Users.Add(moe);
            }

            response = new Response();
            //response = Response.AsJson<JMMContracts.PlexAndKodi.PlexContract_Users>(plexUsers);
            response = Response.AsJson((List<ExpandoObject>)Users);
            response.ContentType = "application/json";
            return response;
        }

        private object GetVersion()
        {
            respond = _impl.GetVersion(_prov);
            response = new Response();
            response = Response.AsJson<JMMContracts.PlexAndKodi.Response>(respond);
            response.ContentType = "application/json";
            return response;
        }

        private object Search(string uid, string limit, string query, bool searchTag)
        {
            media = _impl.Search(_prov, uid, limit, query, searchTag);
            api_media = (JMMContracts.API.API_MediaContainer)media;
            response = new Response();
            response = Response.AsJson<JMMContracts.API.API_MediaContainer>(api_media);
            response.ContentType = "application/json";
            return response;
        }

        //TODO API: once uid is string once its int
        private object GetItemsFromGroup(int uid, string gid)
        {
            info = new PlexAndKodi.BreadCrumbs();
            api_media = (JMMContracts.API.API_MediaContainer)media;
            response = new Response();
            response = Response.AsJson<JMMContracts.API.API_MediaContainer>(api_media);
            response.ContentType = "application/json";
            return response;
        }

        private object ToggleWatchedStatusOnEpisode(string uid, string epid, string status)
        {
            respond = _impl.ToggleWatchedStatusOnEpisode(_prov, uid, epid, status);
            response = new Response();
            response = Response.AsJson<JMMContracts.PlexAndKodi.Response>(respond);
            response.ContentType = "application/json";
            return response;
        }

        private object VoteAnime(string uid, string objid, string votevalue, string votetype)
        {
            respond = _impl.VoteAnime(_prov, uid, objid, votevalue, votetype);
            response = new Response();
            response = Response.AsJson<JMMContracts.PlexAndKodi.Response>(respond);
            response.ContentType = "application/json";
            return response;
        }

        private object TraktScrobble(string animeid, string type, string progress, string status)
        {
            respond = _impl.TraktScrobble(_prov, animeid, type, progress, status);
            response = new Response();
            response = Response.AsJson<JMMContracts.PlexAndKodi.Response>(respond);
            response.ContentType = "application/json";
            return response;
        }
        
        private object GetItemsFromSerie(int uid, string serieid)
        {
            info = new PlexAndKodi.BreadCrumbs();
            media = _impl.GetItemsFromSerie(_prov, uid, serieid, info);
            api_media = (JMMContracts.API.API_MediaContainer)media;
            response = new Response();
            response = Response.AsJson<JMMContracts.API.API_MediaContainer>(api_media);
            response.ContentType = "application/json";
            return response;
        }
    }
}
