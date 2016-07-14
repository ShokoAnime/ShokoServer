using System;
using Nancy;
using System.Dynamic;
using System.Collections.Generic;
using JMMContracts.API;

namespace JMMServer.API
{
    //class will be found automagicly thanks to inherits also class need to be public (or it will 404)
    public class API_Calls: Nancy.NancyModule
    {
        public API_Calls()
        {
            // CommonImplementation
            Get["/"] = parameter => { return IndexPage; };
            Get["/JMMServerKodi/GetSupportImage/{name}"] = parameter => { return GetSupportImage(parameter.name); };
            Get["/JMMServerKodi/GetFilters/{uid}"] = parameter => { return GetFilters(parameter.uid); };
            Get["/JMMServerKodi/GetMetadata/{uid}/{type}/{id}/{historyinfo}"] = parameter => { return GetMetadata(parameter.uid, parameter.type, parameter.id, parameter.historyinfo); };
            Get["/JMMServerKodi/GetUsers"] = parameter => { return GetUsers(); };
            Get["/JMMServerKodi/GetVersion"] = parameter => { return GetVersion(); };
            Get["/JMMServerKodi/Search/{uid}/{limit}/{query}/{searchTag}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, parameter.searchTag); };
            Get["/JMMServerKodi/GetItemsFromGroup/{uid}/{gid}"] = parameter => { return GetItemsFromGroup(parameter.uid, parameter.gid); };
            Get["/JMMServerKodi/ToggleWatchedStatusOnEpisode/{uid}/{epid}/{status}"] = parameter => { return ToggleWatchedStatusOnEpisode(parameter.uid, parameter.epid, parameter.status); };
            Get["/JMMServerKodi/VoteAnime/{uid}/{objid}/{votevalue}/{votetype}"] = parameter => { return VoteAnime(parameter.uid, parameter.objid, parameter.votevalue, parameter.votetype); };
            Get["/JMMServerKodi/TraktScrobble/{animeid}/{type}/{progress}/{status}"] = parameter => { return TraktScrobble(parameter.animeid, parameter.type, parameter.progress, parameter.status); };
            Get["/JMMServerKodi/GetItemsFromSerie/{uid}/{serieid}"] = paramter => { return GetItemsFromSerie(paramter.uid, paramter.serieid); };

            // KodiImplementation
            Get["/GetMetadata/{uid}/{type}/{id}"] = parameter => { return GetMetadata(parameter.uid, parameter.type, parameter.id, null); };
            Get["/Search/{uid}/{limit}/{query}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, false); };
            Get["/SearchTag/{uid}/{limit}/{query}"] = parameter => { return Search(parameter.uid, parameter.limit, parameter.query, true); };

            // PlexImplementation
            // nothing specific only provider

            // JMMServerRest
            Get["/JMMServerREST/GetImage/{type}/{id}"] = parameter => { return GetImage(parameter.type, parameter.id); };
            Get["/JMMServerREST/GetThumb/{type}/{id}/{ratio}"] = parameter => { return GetThumb(parameter.type, parameter.id, parameter.ratio); };
            Get["/JMMServerREST/GetSupportImage/{name}/{ratio}"] = parameter => { return GetSupportImage(parameter.name, parameter.ratio); };
            Get["/JMMServerREST/GetImageUsingPath/{path}"] = parameter => { return GetImageUsingPath(parameter.path); };

            // IJMMServerKodi
            Get["/JMMServerKodi/GetMetadata/{uid}/{type}/{id}"] = parameter => { return GetMetadata(parameter.uid, parameter.type, parameter.id, null); };
        }

        const String IndexPage = @"<html><body><h1>JMMServer is running</h1></body></html>";

        //TODO API: _prov should change probably on path/road for different function to trigger kodi/plex
        PlexAndKodi.IProvider _prov = new PlexAndKodi.Kodi.KodiProvider();
        PlexAndKodi.CommonImplementation _impl = new PlexAndKodi.CommonImplementation();
        JMMServiceImplementationREST _rest = new JMMServiceImplementationREST();
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
            response = Response.FromStream(image, "image/png");
            return response;
        }
        
        private object GetMetadata(string uid, string typeid, string id, string historyinfo)
        {
            media = _impl.GetMetadata(_prov, uid, typeid, id, historyinfo);
            api_media = (JMMContracts.API.API_MediaContainer)media;

            dynamic Series = new ExpandoObject();
            List<ExpandoObject> series = new List<ExpandoObject>();

            switch (typeid)
            {
                case "5":
                case "3":
                    //episodes
                    
                    foreach (API_Video vid in api_media.Childrens)
                    {
                        //TODO API: still need to add data to respond
                        moe = new ExpandoObject();
                        moe.id = vid.Id;
                        moe.title = vid.Title;
                        moe.episode = vid.EpisodeNumber;
                        moe.season = vid.Season;
                        moe.rating = vid.Rating;
                        //votes
                        //my_rating
                        moe.datastart = vid.OriginallyAvailableAt;
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
                        //moe.source = vid.SourceTitle;
                        moe.watched = vid.ViewedLeafCount;

                        dynamic Media = new List<ExpandoObject>();
                        dynamic moe_media = new ExpandoObject();
                        if (vid.Medias != null)
                        {
                            foreach (API_Media media in vid.Medias)
                            {
                                dynamic Parts = new List<ExpandoObject>();
                                dynamic moe_part = new ExpandoObject();

                                if (media.Parts != null)
                                {
                                    foreach (API_Part part in media.Parts)
                                    {
                                        moe_part = new ExpandoObject();

                                        dynamic Stream = new List<ExpandoObject>();
                                        dynamic moe_stream = new ExpandoObject();
                                        if (part.Streams != null)
                                        {
                                            foreach (API_Stream stream in part.Streams)
                                            {
                                                moe_stream = new ExpandoObject();
                                                moe_stream.id = stream.Id;
                                                moe_stream.type = stream.StreamType;
                                                switch (stream.StreamType)
                                                {
                                                    case "1":
                                                        //video
                                                        moe_stream.VideoCodec = stream.Codec;
                                                        moe_stream.width = stream.Width;
                                                        moe_stream.height = stream.Height;
                                                        moe_stream.duration = stream.Duration;
                                                        break;
                                                    case "2":
                                                        //audio
                                                        moe_stream.AudioCodec = stream.Codec;
                                                        moe_stream.AudioLanguage = stream.Language;
                                                        moe_stream.AudioChannels = stream.Channels;
                                                        break;
                                                    case "3":
                                                        //sub
                                                        moe_stream.Language = stream.Language;
                                                        break;
                                                }
                                                Stream.Add(moe_stream);
                                            }
                                            moe_part.key = part.Key;
                                            moe_part.part = Stream;
                                        }
                                        Parts.Add(moe_part);
                                    }
                                    moe_media.parts = Parts;
                                }
                                Media.Add(moe_media);
                            }
                            moe.media = Media;
                        }
                        
                        //finaly add series to the list for respond
                        series.Add(moe);
                    }

                    //Tags and Role are exclusive for series
                    //TODO API: this is a dirty hack, need to change MediaContainer to fix this
                    dynamic tags = new List<ExpandoObject>();
                    if (api_media.Childrens[0].Tags != null)
                    {
                        foreach (API_Tag tag in api_media.Childrens[0].Tags)
                        {
                            tags.Add(tag);
                        }
                    }
                    moe.tag = tags;

                    dynamic Role = new List<ExpandoObject>();
                    dynamic moe_tag = new ExpandoObject();
                    if (api_media.Childrens[0].Roles != null)
                    {
                        foreach (API_RoleTag role in api_media.Childrens[0].Roles)
                        {
                            //dynamic need to be recreate to not duplicate values
                            moe_tag = new ExpandoObject();
                            moe_tag.actor = role.Value;
                            moe_tag.actorpic = role.TagPicture;
                            moe_tag.role = role.Role;
                            moe_tag.roledesc = role.RoleDescription;
                            moe_tag.rolepic = role.RolePicture;
                            Role.Add(moe_tag);
                        }
                    }
                    moe.cast = Role;

                    dynamic Genre = new List<string>();
                    if (api_media.Childrens[0].Genres != null)
                    {
                        foreach (API_Tag genre in api_media.Childrens[0].Genres)
                        {
                            Genre.Add(genre.Value);
                        }
                    }
                    moe.genre = Genre;

                    moe.year = api_media.Childrens[0].Year;

                    Series.count = media.Size;
                    Series.episodes = series;
                    break;
            
            case "0":
            default:
            foreach (API_Video vid in api_media.Childrens)
                {
                    //TODO API: still need to add data to respond
                    moe = new ExpandoObject();
                    moe.id = vid.Id;
                    moe.count_local = vid.ChildCount;
                    //fallback title
                    moe.title = vid.Title;

                    dynamic Titles = new List<ExpandoObject>();
                    dynamic moe_title = new ExpandoObject();
                    if (vid.Titles != null)
                    {
                        foreach (API_AnimeTitle title in vid.Titles)
                        {
                            //dynamic need to be recreate to not duplicate values
                            moe_title = new ExpandoObject();
                            moe_title.lang = title.Language;
                            moe_title.type = title.Type;
                            moe_title.title = title.Title;
                            Titles.Add(moe_title);
                        }
                    }
                    moe.titles = Titles;

                    moe.year = vid.Year;
                    moe.episode = vid.LeafCount;
                    moe.season = vid.Season;
                    moe.rating = vid.Rating;
                    //votes
                    //my_rating
                    moe.datastart = vid.OriginallyAvailableAt;
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
                    dynamic Genre_ = new List<string>();
                    if (vid.Genres != null)
                    {
                        foreach (API_Tag genre in vid.Genres)
                        {
                            Genre_.Add(genre.Value);
                        }
                    }
                    moe.genre = Genre_;

                    dynamic Role_ = new List<ExpandoObject>();
                    dynamic moe_tag_ = new ExpandoObject();
                    if (vid.Roles != null)
                    {
                        foreach (API_RoleTag role in vid.Roles)
                        {
                            //dynamic need to be recreate to not duplicate values
                            moe_tag_ = new ExpandoObject();
                            moe_tag_.actor = role.Value;
                            moe_tag_.actorpic = role.TagPicture;
                            moe_tag_.role = role.Role;
                            moe_tag_.roledesc = role.RoleDescription;
                            moe_tag_.rolepic = role.RolePicture;
                            Role_.Add(moe_tag_);
                        }
                    }
                    moe.cast = Role_;

                    dynamic tags_ = new List<ExpandoObject>();
                    if (vid.Tags != null)
                    {
                        foreach (API_Tag tag in vid.Tags)
                        {
                            tags_.Add(tag);
                        }
                    }
                    moe.tag = tags_;

                    //finaly add series to the list for respond
                    series.Add(moe);
                }
                    Series.count = media.Size;
                    Series.series = series;
                    break;
            }

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

        //REST
        private object GetImage(string type, string id)
        {
            image = _rest.GetImage(type, id);
            response = new Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

        private object GetThumb(string type, string id, string ratio)
        {
            image = _rest.GetThumb(type, id, ratio);
            response = new Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

        private object GetSupportImage(string name, string ratio)
        {
            image = _rest.GetSupportImage(name, ratio);
            response = new Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

        private object GetImageUsingPath(string path)
        {
            image = _rest.GetImageUsingPath(path);
            response = new Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }
    }
}
