using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Plex.Connections;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Plex;
using Shoko.Server.PlexAndKodi.Kodi;
using Shoko.Server.PlexAndKodi.Plex;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Directory = Shoko.Models.Plex.Libraries.Directory;
using MediaContainer = Shoko.Models.PlexAndKodi.MediaContainer;
using Stream = System.IO.Stream;

// ReSharper disable FunctionComplexityOverflow

namespace Shoko.Server.PlexAndKodi;

public class CommonImplementation
{
    private readonly ILogger<CommonImplementation> _logger;
    private readonly ISettingsProvider _settingsProvider;

    //private functions are use internal

    public CommonImplementation(ILogger<CommonImplementation> logger, ISettingsProvider settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
    }

    public Stream GetSupportImage(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        name = Path.GetFileNameWithoutExtension(name);
        var man = Resources.ResourceManager;
        var dta = (byte[])man.GetObject(name);
        if (dta == null || dta.Length == 0)
        {
            return null;
        }

        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public MediaContainer GetFilters(IProvider prov, string uid)
    {
        int.TryParse(uid, out var t);
        var user = t > 0 ? Helper.GetJMMUser(uid) : Helper.GetUser(uid);
        if (user == null)
        {
            return new MediaContainer { ErrorString = "User not found" };
        }

        var userid = user.JMMUserID;

        var info = prov.UseBreadCrumbs
            ? new BreadCrumbs { Key = prov.ConstructFiltersUrl(userid), Title = "Anime" }
            : null;
        var ret =
            new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Show, "Anime", false, false, info));
        if (!ret.Init(prov))
        {
            return new MediaContainer(); //Normal OPTION VERB
        }

        var dirs = new List<Video>();
        try
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var allGfs = RepoFactory.GroupFilter.GetTopLevel()
                    .Where(a => !a.IsHidden &&
                                (
                                    (a.GroupsIds.ContainsKey(userid) && a.GroupsIds[userid].Count > 0)
                                    || a.IsDirectory)
                    )
                    .ToList();


                foreach (var gg in allGfs)
                {
                    var pp = Helper.DirectoryFromFilter(prov, gg, userid);
                    if (pp != null)
                    {
                        dirs.Add(prov, pp, info);
                    }
                }

                var vids = RepoFactory.VideoLocal.GetVideosWithoutEpisodeUnsorted();
                if (vids.Count > 0)
                {
                    var pp = new Shoko.Models.PlexAndKodi.Directory { Type = "show" };
                    pp.Key = prov.ShortUrl(prov.ConstructUnsortUrl(userid));
                    pp.Title = "Unsort";
                    pp.AnimeType = AnimeTypes.AnimeUnsort.ToString();
                    pp.Thumb = prov.ConstructSupportImageLink("plex_unsort.png");
                    pp.LeafCount = vids.Count;
                    pp.ViewedLeafCount = 0;
                    dirs.Add(prov, pp, info);
                }

                var playlists = RepoFactory.Playlist.GetAll();
                if (playlists.Count > 0)
                {
                    var pp = new Shoko.Models.PlexAndKodi.Directory { Type = "show" };
                    pp.Key = prov.ShortUrl(prov.ConstructPlaylistUrl(userid));
                    pp.Title = "Playlists";
                    pp.AnimeType = AnimeTypes.AnimePlaylist.ToString();
                    pp.Thumb = prov.ConstructSupportImageLink("plex_playlists.png");
                    pp.LeafCount = playlists.Count;
                    pp.ViewedLeafCount = 0;
                    dirs.Add(prov, pp, info);
                }

                dirs = dirs.OrderBy(a => a.Title).ToList();
            }

            ret.MediaContainer.RandomizeArt(prov, dirs);
            if (prov.AddPlexPrefsItem)
            {
                var dir = new Shoko.Models.PlexAndKodi.Directory { Prompt = "Search" };
                dir.Thumb = dir.Art = "/:/plugins/com.plexapp.plugins.myanime/resources/Search.png";
                dir.Key = "/video/jmm/search";
                dir.Title = "Search";
                dir.Search = "1";
                dirs.Add(dir);
            }

            if (prov.AddPlexSearchItem)
            {
                var dir = new Shoko.Models.PlexAndKodi.Directory();
                dir.Thumb = dir.Art = "/:/plugins/com.plexapp.plugins.myanime/resources/Gear.png";
                dir.Key = "/:/plugins/com.plexapp.plugins.myanime/prefs";
                dir.Title = "Preferences";
                dir.Settings = "1";
                dirs.Add(dir);
            }

            ret.Childrens = dirs;
            var dinfo = prov.GetPlexClient();
            if (dinfo != null)
            {
                _logger.LogInformation(dinfo.ToString());
            }

            return ret.GetStream(prov);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new MediaContainer { ErrorString = "System Error, see JMMServer logs for more information" };
        }
    }

    public MediaContainer GetMetadata(IProvider prov, string UserId, int type, string Id, string historyinfo,
        bool nocast = false, int? filter = null)
    {
        try
        {
            var his = prov.UseBreadCrumbs ? BreadCrumbs.FromKey(historyinfo) : null;
            var user = Helper.GetJMMUser(UserId);

            switch ((JMMType)type)
            {
                case JMMType.Group:
                    return GetItemsFromGroup(prov, user.JMMUserID, Id, his, nocast, filter);
                case JMMType.GroupFilter:
                    return GetGroupsOrSubFiltersFromFilter(prov, user.JMMUserID, Id, his, nocast);
                case JMMType.GroupUnsort:
                    return GetUnsort(prov, user.JMMUserID, his);
                case JMMType.Serie:
                    return GetItemsFromSerie(prov, user.JMMUserID, Id, his, nocast);
                case JMMType.Episode:
                    return GetFromEpisode(prov, user.JMMUserID, Id, his);
                case JMMType.File:
                    return GetFromFile(prov, user.JMMUserID, Id, his);
                case JMMType.Playlist:
                    return GetItemsFromPlaylist(prov, user.JMMUserID, Id, his);
                case JMMType.FakeIosThumb:
                    return FakeParentForIOSThumbnail(prov, Id);
            }

            return new MediaContainer { ErrorString = "Unsupported Type" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new MediaContainer { ErrorString = "System Error, see JMMServer logs for more information" };
        }
    }

    private MediaContainer GetItemsFromPlaylist(IProvider prov, int userid, string id, BreadCrumbs info)
    {
        var PlaylistID = -1;
        int.TryParse(id, out PlaylistID);

        if (PlaylistID == 0)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var ret = new BaseObject(
                    prov.NewMediaContainer(MediaContainerTypes.Show, "Playlists", true, true, info));
                if (!ret.Init(prov))
                {
                    return new MediaContainer(); //Normal
                }

                var retPlaylists = new List<Video>();
                var playlists = RepoFactory.Playlist.GetAll();
                var sessionWrapper = session.Wrap();

                foreach (var playlist in playlists)
                {
                    var dir = new Shoko.Models.PlexAndKodi.Directory
                    {
                        Key = prov.ShortUrl(prov.ConstructPlaylistIdUrl(userid, playlist.PlaylistID)),
                        Title = playlist.PlaylistName,
                        Id = playlist.PlaylistID,
                        AnimeType = AnimeTypes.AnimePlaylist.ToString()
                    };
                    var episodeID = -1;
                    if (int.TryParse(playlist.PlaylistItems.Split('|')[0].Split(';')[1], out episodeID))
                    {
                        var anime = RepoFactory.AnimeEpisode.GetByID(episodeID)
                            .GetAnimeSeries()
                            .GetAnime();
                        dir.Thumb = anime?.GetDefaultPosterDetailsNoBlanks()?.GenPoster(prov);
                        dir.Art = anime?.GetDefaultFanartDetailsNoBlanks()?.GenArt(prov);
                        dir.Banner = anime?.GetDefaultWideBannerDetailsNoBlanks()?.GenArt(prov);
                    }
                    else
                    {
                        dir.Thumb = prov.ConstructSupportImageLink("plex_404V.png");
                    }

                    dir.LeafCount = playlist.PlaylistItems.Split('|').Count();
                    dir.ViewedLeafCount = 0;
                    retPlaylists.Add(prov, dir, info);
                }

                retPlaylists = retPlaylists.OrderBy(a => a.Title).ToList();
                ret.Childrens = retPlaylists;
                return ret.GetStream(prov);
            }
        }

        if (PlaylistID > 0)
        {
            var playlist = RepoFactory.Playlist.GetByID(PlaylistID);
            var playlistItems = playlist.PlaylistItems.Split('|');
            var vids = new List<Video>();
            var ret =
                new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Episode, playlist.PlaylistName, true,
                    true,
                    info));
            if (!ret.Init(prov))
            {
                return new MediaContainer(); //Normal
            }

            foreach (var item in playlistItems)
            {
                try
                {
                    var episodeID = -1;
                    int.TryParse(item.Split(';')[1], out episodeID);
                    if (episodeID < 0)
                    {
                        return new MediaContainer { ErrorString = "Invalid Episode ID" };
                    }

                    var e = RepoFactory.AnimeEpisode.GetByID(episodeID);
                    if (e == null)
                    {
                        return new MediaContainer { ErrorString = "Invalid Episode" };
                    }

                    var ep =
                        new KeyValuePair<SVR_AnimeEpisode, CL_AnimeEpisode_User>(e,
                            e.GetUserContract(userid));
                    if (ep.Value != null && ep.Value.LocalFileCount == 0)
                    {
                        continue;
                    }

                    var ser = RepoFactory.AnimeSeries.GetByID(ep.Key.AnimeSeriesID);
                    if (ser == null)
                    {
                        return new MediaContainer { ErrorString = "Invalid Series" };
                    }

                    var con = ser.GetUserContract(userid);
                    if (con == null)
                    {
                        return new MediaContainer { ErrorString = "Invalid Series, Contract not found" };
                    }

                    var v = Helper.VideoFromAnimeEpisode(prov, con.CrossRefAniDBTvDBV2, ep, userid);
                    if (v != null && v.Medias != null && v.Medias.Count > 0)
                    {
                        Helper.AddInformationFromMasterSeries(v, con, ser.GetPlexContract(userid));
                        v.Type = "episode";
                        vids.Add(prov, v, info);
                        var dinfo = prov.GetPlexClient();
                        if (prov.ConstructFakeIosParent && dinfo != null && dinfo.Client == PlexClient.IOS)
                        {
                            v.GrandparentKey =
                                prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v.ParentThumb,
                                    v.Art ?? v.ParentArt ?? v.GrandparentArt));
                        }

                        v.ParentKey = null;
                    }
                }
                catch
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }
            }

            ret.MediaContainer.RandomizeArt(prov, vids);
            ret.Childrens = vids;
            return ret.GetStream(prov);
        }

        return new MediaContainer { ErrorString = "Invalid Playlist" };
    }

    private MediaContainer GetUnsort(IProvider prov, int userid, BreadCrumbs info)
    {
        var ret =
            new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Video, "Unsort", true, true, info));
        if (!ret.Init(prov))
        {
            return new MediaContainer();
        }

        var dirs = new List<Video>();
        var vids = RepoFactory.VideoLocal.GetVideosWithoutEpisode();
        foreach (var v in vids.OrderByDescending(a => a.DateTimeCreated))
        {
            try
            {
                var m = Helper.VideoFromVideoLocal(prov, v, userid);
                dirs.Add(prov, m, info);
                m.Thumb = prov.ConstructSupportImageLink("plex_404.png");
                m.ParentThumb = prov.ConstructSupportImageLink("plex_unsort.png");
                m.ParentKey = null;
                var dinfo = prov.GetPlexClient();
                if (prov.ConstructFakeIosParent && dinfo != null && dinfo.Client == PlexClient.IOS)

                {
                    m.GrandparentKey =
                        prov.Proxyfy(prov.ConstructFakeIosThumb(userid, m.ParentThumb,
                            m.Art ?? m.ParentArt ?? m.GrandparentArt));
                }
            }
            catch
            {
                //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
            }
        }

        ret.Childrens = dirs;
        return ret.GetStream(prov);
    }

    private MediaContainer GetFromFile(IProvider prov, int userid, string Id, BreadCrumbs info)
    {
        if (!int.TryParse(Id, out var id))
        {
            return new MediaContainer { ErrorString = "Invalid File Id" };
        }

        var vi = RepoFactory.VideoLocal.GetByID(id);
        var ret =
            new BaseObject(prov.NewMediaContainer(MediaContainerTypes.File,
                Path.GetFileNameWithoutExtension(vi.FileName ?? string.Empty),
                true, false, info));
        var v2 = Helper.VideoFromVideoLocal(prov, vi, userid);
        var dirs = new List<Video>();
        dirs.EppAdd(prov, v2, info, true);
        v2.Thumb = prov.ConstructSupportImageLink("plex_404.png");
        v2.ParentThumb = prov.ConstructSupportImageLink("plex_unsort.png");
        var dinfo = prov.GetPlexClient();
        if (prov.ConstructFakeIosParent && dinfo != null && dinfo.Client == PlexClient.IOS)

        {
            v2.GrandparentKey =
                prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v2.ParentThumb,
                    v2.Art ?? v2.ParentArt ?? v2.GrandparentArt));
        }

        v2.ParentKey = null;
        if (prov.UseBreadCrumbs)
        {
            v2.Key = prov.ShortUrl(ret.MediaContainer.Key);
        }

        ret.MediaContainer.Childrens = dirs;
        return ret.GetStream(prov);
    }

    private MediaContainer GetFromEpisode(IProvider prov, int userid, string Id, BreadCrumbs info)
    {
        if (!int.TryParse(Id, out var id))
        {
            return new MediaContainer { ErrorString = "Invalid Episode Id" };
        }

        var ret =
            new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Episode, "Episode", true, true, info));
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            var dirs = new List<Video>();
            var sessionWrapper = session.Wrap();

            var e = RepoFactory.AnimeEpisode.GetByID(id);
            if (e == null)
            {
                return new MediaContainer { ErrorString = "Invalid Episode Id" };
            }

            var ep =
                new KeyValuePair<SVR_AnimeEpisode, CL_AnimeEpisode_User>(e,
                    e.GetUserContract(userid));
            if (ep.Value != null && ep.Value.LocalFileCount == 0)
            {
                return new MediaContainer { ErrorString = "Episode do not have videolocals" };
            }

            var aep = ep.Key.AniDB_Episode;
            if (aep == null)
            {
                return new MediaContainer { ErrorString = "Invalid Episode AniDB link not found" };
            }

            var ser = RepoFactory.AnimeSeries.GetByID(ep.Key.AnimeSeriesID);
            if (ser == null)
            {
                return new MediaContainer { ErrorString = "Invalid Serie" };
            }

            var anime = ser.GetAnime();
            var con = ser.GetUserContract(userid);
            if (con == null)
            {
                return new MediaContainer { ErrorString = "Invalid Serie, Contract not found" };
            }

            try
            {
                var v = Helper.VideoFromAnimeEpisode(prov, con.CrossRefAniDBTvDBV2, ep, userid);
                if (v != null)
                {
                    var nv = ser.GetPlexContract(userid);
                    Helper.AddInformationFromMasterSeries(v, con, ser.GetPlexContract(userid),
                        prov is KodiProvider);
                    if (v.Medias != null && v.Medias.Count > 0)
                    {
                        v.Type = "episode";
                        dirs.EppAdd(prov, v, info, true);
                        var dinfo = prov.GetPlexClient();
                        if (prov.ConstructFakeIosParent && dinfo != null && dinfo.Client == PlexClient.IOS)
                        {
                            v.GrandparentKey =
                                prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v.ParentThumb,
                                    v.Art ?? v.ParentArt ?? v.GrandparentArt));
                        }

                        v.ParentKey = null;
                    }

                    if (prov.UseBreadCrumbs)
                    {
                        v.Key = prov.ShortUrl(ret.MediaContainer.Key);
                    }

                    ret.MediaContainer.Art = prov.ReplaceSchemeHost(nv.Art ?? nv.ParentArt ?? nv.GrandparentArt);
                }

                ret.MediaContainer.Childrens = dirs;
                return ret.GetStream(prov);
            }
            catch
            {
                //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
            }
        }

        return new MediaContainer { ErrorString = "Episode Not Found" };
    }

    public Dictionary<int, string> GetUsers()
    {
        var users = new Dictionary<int, string>();
        try
        {
            foreach (var us in RepoFactory.JMMUser.GetAll())
            {
                users.Add(us.JMMUserID, us.Username);
            }

            return users;
        }
        catch
        {
            return null;
        }
    }

    public PlexContract_Users GetUsers(IProvider prov)
    {
        var gfs = new PlexContract_Users();
        try
        {
            gfs.Users = new List<PlexContract_User>();
            foreach (var us in RepoFactory.JMMUser.GetAll())
            {
                var p = new PlexContract_User { id = us.JMMUserID.ToString(), name = us.Username };
                gfs.Users.Add(p);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new PlexContract_Users { ErrorString = "System Error, see JMMServer logs for more information" };
        }

        return gfs;
    }

    public Response GetVersion()
    {
        var rsp = new Response();
        try
        {
            rsp.Code = "200";
            rsp.Message = Assembly.GetEntryAssembly().GetName().Version.ToString();
            return rsp;
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.ToString());
            rsp.Code = "500";
            rsp.Message = "System Error, see JMMServer logs for more information";
        }

        return rsp;
    }

    public MediaContainer Search(IProvider prov, string UserId, int lim, string query, bool searchTag,
        bool nocast = false)
    {
        var info = prov.UseBreadCrumbs
            ? new BreadCrumbs
            {
                Key = prov.ConstructSearchUrl(UserId, lim, query, searchTag), Title = "Search for '" + query + "'"
            }
            : null;

        var ret =
            new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Show, "Search for '" + query + "'", true,
                true,
                info));
        if (lim == 0)
        {
            lim = 100;
        }

        var user = Helper.GetUser(UserId);
        if (user == null)
        {
            return new MediaContainer { ErrorString = "User Not Found" };
        }

        var ls = new List<Video>();
        var cnt = 0;
        var series = searchTag
            ? RepoFactory.AnimeSeries.GetAll()
                .Where(
                    a =>
                        a.Contract != null && a.Contract.AniDBAnime != null &&
                        a.Contract.AniDBAnime.AniDBAnime != null &&
                        (a.Contract.AniDBAnime.AniDBAnime.GetAllTags()
                             .Contains(query,
                                 StringComparer.InvariantCultureIgnoreCase) ||
                         a.Contract.AniDBAnime.CustomTags.Select(b => b.TagName)
                             .Contains(query, StringComparer.InvariantCultureIgnoreCase)))
            : RepoFactory.AnimeSeries.GetAll()
                .Where(
                    a =>
                        a.Contract != null && a.Contract.AniDBAnime != null &&
                        a.Contract.AniDBAnime.AniDBAnime != null &&
                        string.Join(",", a.Contract.AniDBAnime.AniDBAnime.AllTitles)
                            .IndexOf(query, 0, StringComparison.InvariantCultureIgnoreCase) >= 0);

        //List<AniDB_Anime> animes = searchTag ? RepoFactory.AniDB_Anime.SearchByTag(query) : RepoFactory.AniDB_Anime.SearchByName(query);
        foreach (var ser in series)
        {
            if (!user.AllowedSeries(ser))
            {
                continue;
            }

            Video v = ser.GetPlexContract(user.JMMUserID)?.Clone<Shoko.Models.PlexAndKodi.Directory>(prov);
            if (v != null)
            {
                switch (ser.Contract.AniDBAnime.AniDBAnime.AnimeType)
                {
                    case (int)AnimeType.Movie:
                        v.SourceTitle = "Anime Movies";
                        break;
                    case (int)AnimeType.OVA:
                        v.SourceTitle = "Anime Ovas";
                        break;
                    case (int)AnimeType.Other:
                        v.SourceTitle = "Anime Others";
                        break;
                    case (int)AnimeType.TVSeries:
                        v.SourceTitle = "Anime Series";
                        break;
                    case (int)AnimeType.TVSpecial:
                        v.SourceTitle = "Anime Specials";
                        break;
                    case (int)AnimeType.Web:
                        v.SourceTitle = "Anime Web Clips";
                        break;
                }

                if (nocast)
                {
                    v.Roles = null;
                }

                v.GenerateKey(prov, user.JMMUserID);
                ls.Add(prov, v, info);
            }

            cnt++;
            if (cnt == lim)
            {
                break;
            }
        }

        ret.MediaContainer.RandomizeArt(prov, ls);
        ret.MediaContainer.Childrens = Helper.ConvertToDirectory(ls, prov);
        return ret.GetStream(prov);
    }

    public MediaContainer GetItemsFromGroup(IProvider prov, int userid, string GroupId, BreadCrumbs info,
        bool nocast, int? filterID)
    {
        int.TryParse(GroupId, out var groupID);
        if (groupID == -1)
        {
            return new MediaContainer { ErrorString = "Invalid Group Id" };
        }

        var retGroups = new List<Video>();
        var grp = RepoFactory.AnimeGroup.GetByID(groupID);

        if (grp == null)
        {
            return new MediaContainer { ErrorString = "Invalid Group" };
        }

        var ret =
            new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Show, grp.GroupName, false, true, info));
        if (!ret.Init(prov))
        {
            return new MediaContainer();
        }

        var basegrp = grp?.GetUserContract(userid);
        if (basegrp != null)
        {
            var seriesList = grp.GetSeries();
            if (filterID != null)
            {
                var filter = RepoFactory.GroupFilter.GetByID(filterID.Value);
                if (filter != null)
                {
                    if (filter.ApplyToSeries > 0)
                    {
                        if (filter.SeriesIds.ContainsKey(userid))
                        {
                            seriesList =
                                seriesList.Where(a => filter.SeriesIds[userid].Contains(a.AnimeSeriesID)).ToList();
                        }
                    }
                }
            }

            foreach (var grpChild in grp.GetChildGroups())
            {
                var v = grpChild.GetPlexContract(userid);
                if (v != null)
                {
                    v.Type = "show";
                    v.GenerateKey(prov, userid);

                    v.Art = Helper.GetRandomFanartFromVideo(v, prov) ?? v.Art;
                    v.Banner = Helper.GetRandomBannerFromVideo(v, prov) ?? v.Banner;

                    if (nocast)
                    {
                        v.Roles = null;
                    }

                    retGroups.Add(prov, v, info);
                    v.ParentThumb = v.GrandparentThumb = null;
                }
            }

            foreach (var ser in seriesList)
            {
                var v = ser.GetPlexContract(userid)?.Clone<Shoko.Models.PlexAndKodi.Directory>(prov);
                if (v != null)
                {
                    v.AirDate = ser.AirDate ?? DateTime.MinValue;
                    v.Group = basegrp;
                    v.Type = "show";
                    v.GenerateKey(prov, userid);
                    v.Art = Helper.GetRandomFanartFromVideo(v, prov) ?? v.Art;
                    v.Banner = Helper.GetRandomBannerFromVideo(v, prov) ?? v.Banner;
                    if (nocast)
                    {
                        v.Roles = null;
                    }

                    retGroups.Add(prov, v, info);
                    v.ParentThumb = v.GrandparentThumb = null;
                }
            }
        }

        ret.MediaContainer.RandomizeArt(prov, retGroups);
        ret.Childrens = Helper.ConvertToDirectory(retGroups.OrderBy(a => a.AirDate).ToList(), prov);
        FilterExtras(prov, ret.Childrens);
        return ret.GetStream(prov);
    }

    public Response ToggleWatchedStatusOnEpisode(IProvider prov, string userid, int aep, bool wstatus)
    {
        var rsp = new Response { Code = "400", Message = "Bad Request" };
        try
        {
            if (!int.TryParse(userid, out var usid))
            {
                return rsp;
            }

            var ep = RepoFactory.AnimeEpisode.GetByID(aep);
            if (ep == null)
            {
                rsp.Code = "404";
                rsp.Message = "Episode Not Found";
                return rsp;
            }

            ep.ToggleWatchedStatus(wstatus, true, DateTime.Now, false, usid, true);
            var series = ep.GetAnimeSeries();
            series?.UpdateStats(true, false);
            series?.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);
            rsp.Code = "200";
            rsp.Message = null;
        }
        catch (Exception ex)
        {
            rsp.Code = "500";
            rsp.Message = "Internal Error : " + ex;
            _logger.LogError(ex, ex.ToString());
        }

        return rsp;
    }

    public Response ToggleWatchedStatusOnSeries(IProvider prov, string userid, int aep,
        bool wstatus)
    {
        //prov.AddResponseHeaders();

        var rsp = new Response { Code = "400", Message = "Bad Request" };
        try
        {
            if (!int.TryParse(userid, out var usid))
            {
                return rsp;
            }

            var series = RepoFactory.AnimeSeries.GetByID(aep);
            if (series == null)
            {
                rsp.Code = "404";
                rsp.Message = "Episode Not Found";
                return rsp;
            }

            var eps = series.GetAnimeEpisodes();
            foreach (var ep in eps)
            {
                if (ep.AniDB_Episode == null)
                {
                    continue;
                }

                if (ep.EpisodeTypeEnum == EpisodeType.Credits)
                {
                    continue;
                }

                if (ep.EpisodeTypeEnum == EpisodeType.Trailer)
                {
                    continue;
                }

                ep.ToggleWatchedStatus(wstatus, true, DateTime.Now, false, usid, true);
            }

            series.UpdateStats(true, false);
            series.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);
            rsp.Code = "200";
            rsp.Message = null;
        }
        catch (Exception ex)
        {
            rsp.Code = "500";
            rsp.Message = "Internal Error : " + ex;
            _logger.LogError(ex, ex.ToString());
        }

        return rsp;
    }

    public Response ToggleWatchedStatusOnGroup(IProvider prov, string userid, int aep,
        bool wstatus)
    {
        //prov.AddResponseHeaders();

        var rsp = new Response { Code = "400", Message = "Bad Request" };
        try
        {
            if (!int.TryParse(userid, out var usid))
            {
                return rsp;
            }

            var group = RepoFactory.AnimeGroup.GetByID(aep);
            if (group == null)
            {
                rsp.Code = "404";
                rsp.Message = "Episode Not Found";
                return rsp;
            }

            foreach (var series in group.GetAllSeries())
            {
                foreach (var ep in series.GetAnimeEpisodes())
                {
                    if (ep.AniDB_Episode == null)
                    {
                        continue;
                    }

                    if (ep.EpisodeTypeEnum == EpisodeType.Credits)
                    {
                        continue;
                    }

                    if (ep.EpisodeTypeEnum == EpisodeType.Trailer)
                    {
                        continue;
                    }

                    ep.ToggleWatchedStatus(wstatus, true, DateTime.Now, false, usid, true);
                }

                series.UpdateStats(true, false);
            }

            group.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, false);

            rsp.Code = "200";
            rsp.Message = null;
        }
        catch (Exception ex)
        {
            rsp.Code = "500";
            rsp.Message = "Internal Error : " + ex;
            _logger.LogError(ex, ex.ToString());
        }

        return rsp;
    }

    public Response VoteAnime(IProvider prov, string userid, int objid, float vvalue,
        int vt)
    {
        var rsp = new Response { Code = "400", Message = "Bad Request" };
        try
        {
            if (!int.TryParse(userid, out var usid))
            {
                return rsp;
            }

            var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
            if (vt == (int)AniDBVoteType.Episode)
            {
                var ep = RepoFactory.AnimeEpisode.GetByID(objid);
                if (ep == null)
                {
                    rsp.Code = "404";
                    rsp.Message = "Episode Not Found";
                    return rsp;
                }

                var anime = ep.GetAnimeSeries().GetAnime();
                if (anime == null)
                {
                    rsp.Code = "404";
                    rsp.Message = "Anime Not Found";
                    return rsp;
                }

                var msg = string.Format("Voting for anime episode: {0} - Value: {1}", ep.AnimeEpisodeID,
                    vvalue);
                _logger.LogInformation(msg);

                // lets save to the database and assume it will work
                var thisVote = RepoFactory.AniDB_Vote.GetByEntityAndType(ep.AnimeEpisodeID, AniDBVoteType.Episode);

                if (thisVote == null)
                {
                    thisVote = new AniDB_Vote { EntityID = ep.AnimeEpisodeID };
                }

                thisVote.VoteType = vt;

                var iVoteValue = 0;
                if (vvalue > 0)
                {
                    iVoteValue = (int)(vvalue * 100);
                }
                else
                {
                    iVoteValue = (int)vvalue;
                }

                msg = string.Format("Voting for anime episode Formatted: {0} - Value: {1}", ep.AnimeEpisodeID,
                    iVoteValue);
                _logger.LogInformation(msg);
                thisVote.VoteValue = iVoteValue;
                RepoFactory.AniDB_Vote.Save(thisVote);

                commandFactory.CreateAndSave<CommandRequest_VoteAnime>(
                    c =>
                    {
                        c.AnimeID = anime.AnimeID;
                        c.VoteType = vt;
                        c.VoteValue = Convert.ToDecimal(vvalue);
                    }
                );
            }

            if (vt == (int)AniDBVoteType.Anime)
            {
                var ser = RepoFactory.AnimeSeries.GetByID(objid);
                var anime = ser.GetAnime();
                if (anime == null)
                {
                    rsp.Code = "404";
                    rsp.Message = "Anime Not Found";
                    return rsp;
                }

                var msg = string.Format("Voting for anime: {0} - Value: {1}", anime.AnimeID, vvalue);
                _logger.LogInformation(msg);

                // lets save to the database and assume it will work
                var thisVote =
                    RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.AnimeTemp) ??
                    RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.Anime);

                if (thisVote == null)
                {
                    thisVote = new AniDB_Vote { EntityID = anime.AnimeID };
                }

                thisVote.VoteType = vt;

                var iVoteValue = 0;
                if (vvalue > 0)
                {
                    iVoteValue = (int)(vvalue * 100);
                }
                else
                {
                    iVoteValue = (int)vvalue;
                }

                msg = string.Format("Voting for anime Formatted: {0} - Value: {1}", anime.AnimeID, iVoteValue);
                _logger.LogInformation(msg);
                thisVote.VoteValue = iVoteValue;
                RepoFactory.AniDB_Vote.Save(thisVote);
                commandFactory.CreateAndSave<CommandRequest_VoteAnime>(
                    c =>
                    {
                        c.AnimeID = anime.AnimeID;
                        c.VoteType = vt;
                        c.VoteValue = Convert.ToDecimal(vvalue);
                    }
                );
            }

            rsp.Code = "200";
            rsp.Message = null;
        }
        catch (Exception ex)
        {
            rsp.Code = "500";
            rsp.Message = "Internal Error : " + ex;
            _logger.LogError(ex, ex.ToString());
        }

        return rsp;
    }

    public Response TraktScrobble(IProvider prov, string animeId, int typeTrakt, float progressTrakt, int status)
    {
        var traktHelper = Utils.ServiceContainer.GetRequiredService<TraktTVHelper>();
        var rsp = new Response { Code = "400", Message = "Bad Request" };
        try
        {
            var statusTraktV2 = ScrobblePlayingStatus.Start;
            switch (status)
            {
                case (int)ScrobblePlayingStatus.Start:
                    statusTraktV2 = ScrobblePlayingStatus.Start;
                    break;
                case (int)ScrobblePlayingStatus.Pause:
                    statusTraktV2 = ScrobblePlayingStatus.Pause;
                    break;
                case (int)ScrobblePlayingStatus.Stop:
                    statusTraktV2 = ScrobblePlayingStatus.Stop;
                    break;
            }

            progressTrakt = progressTrakt / 10;

            switch (typeTrakt)
            {
                // Movie
                case (int)ScrobblePlayingType.movie:
                    rsp.Code = traktHelper.Scrobble(
                            ScrobblePlayingType.movie, animeId,
                            statusTraktV2, progressTrakt)
                        .ToString();
                    rsp.Message = "Movie Scrobbled";
                    break;
                // TV episode
                case (int)ScrobblePlayingType.episode:
                    rsp.Code =
                        traktHelper.Scrobble(ScrobblePlayingType.episode,
                                animeId,
                                statusTraktV2, progressTrakt)
                            .ToString();
                    rsp.Message = "Episode Scrobbled";
                    break;
                //error
            }
        }
        catch (Exception ex)
        {
            rsp.Code = "500";
            rsp.Message = "Internal Error : " + ex;
            _logger.LogError(ex, ex.ToString());
        }

        return rsp;
    }

    private MediaContainer FakeParentForIOSThumbnail(IProvider prov, string base64)
    {
        var ret = new BaseObject(prov.NewMediaContainer(MediaContainerTypes.None, null, false));
        if (!ret.Init(prov))
        {
            return new MediaContainer();
        }

        var urls = Helper.Base64DecodeUrl(base64).Split('|');
        var thumb = prov.ReplaceSchemeHost(urls[0]);
        var art = prov.ReplaceSchemeHost(urls[1]);
        var v = new Shoko.Models.PlexAndKodi.Directory
        {
            Thumb = thumb,
            ParentThumb = thumb,
            GrandparentThumb = thumb,
            Art = art,
            ParentArt = art,
            GrandparentArt = art
        };
        ret.MediaContainer.Thumb = ret.MediaContainer.ParentThumb = ret.MediaContainer.GrandparentThumb = thumb;
        ret.MediaContainer.Art = ret.MediaContainer.ParentArt = ret.MediaContainer.GrandparentArt = art;
        var vids = new List<Video> { v };
        ret.Childrens = vids;
        return ret.GetStream(prov);
    }

    private void FilterExtras(IProvider provider, List<Video> videos)
    {
        foreach (var v in videos)
        {
            if (!provider.EnableAnimeTitlesInLists)
            {
                v.Titles = null;
            }

            if (!provider.EnableGenresInLists)
            {
                v.Genres = null;
            }

            if (!provider.EnableRolesInLists)
            {
                v.Roles = null;
            }
        }
    }

    public MediaContainer GetItemsFromSerie(IProvider prov, int userid, string SerieId, BreadCrumbs info,
        bool nocast)
    {
        BaseObject ret = null;
        EpisodeType? eptype = null;
        int serieID;
        if (SerieId.Contains("_"))
        {
            var ndata = SerieId.Split('_');
            if (!int.TryParse(ndata[0], out var ept))
            {
                return new MediaContainer { ErrorString = "Invalid Serie Id" };
            }

            eptype = (EpisodeType)ept;
            if (!int.TryParse(ndata[1], out serieID))
            {
                return new MediaContainer { ErrorString = "Invalid Serie Id" };
            }
        }
        else
        {
            if (!int.TryParse(SerieId, out serieID))
            {
                return new MediaContainer { ErrorString = "Invalid Serie Id" };
            }
        }


        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            if (serieID == -1)
            {
                return new MediaContainer { ErrorString = "Invalid Serie Id" };
            }

            var sessionWrapper = session.Wrap();
            var ser = RepoFactory.AnimeSeries.GetByID(serieID);
            if (ser == null)
            {
                return new MediaContainer { ErrorString = "Invalid Series" };
            }

            var cseries = ser.GetUserContract(userid);
            if (cseries == null)
            {
                return new MediaContainer { ErrorString = "Invalid Series, Contract Not Found" };
            }

            var nv = ser.GetPlexContract(userid);


            var episodes = ser.GetAnimeEpisodes()
                .ToDictionary(a => a, a => a.GetUserContract(userid));
            episodes = episodes.Where(a => a.Value == null || a.Value.LocalFileCount > 0)
                .ToDictionary(a => a.Key, a => a.Value);
            if (eptype.HasValue)
            {
                episodes = episodes.Where(a => a.Key.AniDB_Episode != null && a.Key.EpisodeTypeEnum == eptype.Value)
                    .ToDictionary(a => a.Key, a => a.Value);
            }
            else
            {
                var types = episodes.Keys.Where(a => a.AniDB_Episode != null)
                    .Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                if (types.Count > 1)
                {
                    ret = new BaseObject(
                        prov.NewMediaContainer(MediaContainerTypes.Show, "Types", false, true, info));
                    if (!ret.Init(prov))
                    {
                        return new MediaContainer();
                    }

                    ret.MediaContainer.Art = cseries.AniDBAnime?.AniDBAnime?.DefaultImageFanart.GenArt(prov);
                    ret.MediaContainer.LeafCount =
                        cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount;
                    ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount;
                    var eps = new List<PlexEpisodeType>();
                    foreach (var ee in types)
                    {
                        var k2 = new PlexEpisodeType();
                        PlexEpisodeType.EpisodeTypeTranslated(k2, ee,
                            (AnimeType)cseries.AniDBAnime.AniDBAnime.AnimeType,
                            episodes.Count(a => a.Key.EpisodeTypeEnum == ee));
                        eps.Add(k2);
                    }

                    var dirs = new List<Video>();
                    //bool converttoseason = true;

                    foreach (var ee in eps.OrderBy(a => a.Name))
                    {
                        Video v = new Shoko.Models.PlexAndKodi.Directory
                        {
                            Art = nv.Art, Title = ee.Name, AnimeType = "AnimeType", LeafCount = ee.Count
                        };
                        v.ChildCount = v.LeafCount;
                        v.ViewedLeafCount = 0;
                        v.Key = prov.ShortUrl(prov.ConstructSerieIdUrl(userid, ee.Type + "_" + ser.AnimeSeriesID));
                        v.Thumb = prov.ConstructSupportImageLink(ee.Image);
                        if (ee.AnimeType == AnimeType.Movie ||
                            ee.AnimeType == AnimeType.OVA)
                        {
                            v = Helper.MayReplaceVideo(v, ser, cseries, userid, false, nv);
                        }

                        dirs.Add(prov, v, info, false, true);
                    }

                    ret.Childrens = dirs;
                    return ret.GetStream(prov);
                }
            }

            ret =
                new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Episode, ser.GetSeriesName(), true,
                    true, info));
            if (!ret.Init(prov))
            {
                return new MediaContainer();
            }

            ret.MediaContainer.Art = cseries.AniDBAnime?.AniDBAnime?.DefaultImageFanart.GenArt(prov);
            ret.MediaContainer.LeafCount =
                cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount;
            ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount;

            // Here we are collapsing to episodes

            var vids = new List<Video>();

            if (eptype.HasValue && info != null)
            {
                info.ParentKey = info.GrandParentKey;
            }

            var hasRoles = false;
            foreach (var ep in episodes)
            {
                try
                {
                    var v = Helper.VideoFromAnimeEpisode(prov, cseries.CrossRefAniDBTvDBV2, ep, userid);
                    if (v != null && v.Medias != null && v.Medias.Count > 0)
                    {
                        if (nocast && !hasRoles)
                        {
                            hasRoles = true;
                        }

                        Helper.AddInformationFromMasterSeries(v, cseries, nv, hasRoles);
                        v.Type = "episode";
                        vids.Add(prov, v, info);
                        v.GrandparentThumb = v.ParentThumb;
                        var dinfo = prov.GetPlexClient();
                        if (prov.ConstructFakeIosParent && dinfo != null && dinfo.Client == PlexClient.IOS)
                        {
                            v.GrandparentKey =
                                prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v.ParentThumb,
                                    v.Art ?? v.ParentArt ?? v.GrandparentArt));
                        }

                        v.ParentKey = null;
                        if (!hasRoles)
                        {
                            hasRoles = true;
                        }
                    }
                }
                catch
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }
            }

            ret.Childrens = vids.OrderBy(a => a.EpisodeNumber).ToList();
            FilterExtras(prov, ret.Childrens);
            return ret.GetStream(prov);
        }
    }

    private MediaContainer GetGroupsOrSubFiltersFromFilter(IProvider prov, int userid, string GroupFilterId,
        BreadCrumbs info, bool nocast)
    {
        //List<Joint> retGroups = new List<Joint>();
        try
        {
            int.TryParse(GroupFilterId, out var groupFilterID);
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var retGroups = new List<Video>();
                if (groupFilterID == -1)
                {
                    return new MediaContainer { ErrorString = "Invalid Group Filter" };
                }

                var start = DateTime.Now;

                SVR_GroupFilter gf;
                gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                if (gf == null)
                {
                    return new MediaContainer { ErrorString = "Invalid Group Filter" };
                }

                var ret =
                    new BaseObject(prov.NewMediaContainer(MediaContainerTypes.Show, gf.GroupFilterName, false, true,
                        info));
                if (!ret.Init(prov))
                {
                    return new MediaContainer();
                }

                var allGfs =
                    RepoFactory.GroupFilter.GetByParentID(groupFilterID)
                        .Where(a => !a.IsHidden &&
                                    (
                                        (a.GroupsIds.ContainsKey(userid) && a.GroupsIds[userid].Count > 0)
                                        || a.IsDirectory)
                        )
                        .ToList();
                var dirs = new List<Shoko.Models.PlexAndKodi.Directory>();
                foreach (var gg in allGfs)
                {
                    var pp = Helper.DirectoryFromFilter(prov, gg, userid);
                    if (pp != null)
                    {
                        dirs.Add(prov, pp, info);
                    }
                }

                if (dirs.Count > 0)
                {
                    ret.Childrens = dirs.OrderBy(a => a.Title).Cast<Video>().ToList();
                    return ret.GetStream(prov);
                }

                var order = new Dictionary<CL_AnimeGroup_User, Video>();
                if (gf.GroupsIds.ContainsKey(userid))
                {
                    // NOTE: The ToList() in the below foreach is required to prevent enumerable was modified exception
                    foreach (var grp in gf.GroupsIds[userid]
                                 .ToList()
                                 .Select(a => RepoFactory.AnimeGroup.GetByID(a))
                                 .Where(a => a != null))
                    {
                        Video v = grp.GetPlexContract(userid)?.Clone<Shoko.Models.PlexAndKodi.Directory>(prov);
                        if (v != null)
                        {
                            if (v.Group == null)
                            {
                                v.Group = grp.GetUserContract(userid);
                            }

                            v.GenerateKey(prov, userid);
                            v.Type = "show";
                            v.Art = Helper.GetRandomFanartFromVideo(v, prov) ?? v.Art;
                            v.Banner = Helper.GetRandomBannerFromVideo(v, prov) ?? v.Banner;
                            if (nocast)
                            {
                                v.Roles = null;
                            }

                            order.Add(v.Group, v);
                            retGroups.Add(prov, v, info);
                            v.ParentThumb = v.GrandparentThumb = null;
                        }
                    }
                }

                ret.MediaContainer.RandomizeArt(prov, retGroups);
                var grps = retGroups.Select(a => a.Group);
                grps = gf.SortCriteriaList.Count != 0
                    ? GroupFilterHelper.Sort(grps, gf)
                    : grps.OrderBy(a => a.GroupName);
                ret.Childrens = grps.Select(a => order[a]).ToList();
                FilterExtras(prov, ret.Childrens);
                return ret.GetStream(prov);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new MediaContainer { ErrorString = "System Error, see JMMServer logs for more information" };
        }
    }

    public void UseDirectories(int userId, List<Directory> directories)
    {
        var settings = _settingsProvider.GetSettings();
        if (directories == null)
        {
            settings.Plex.Libraries = new List<int>();
            return;
        }

        settings.Plex.Libraries = directories.Select(s => s.Key).ToList();
        _settingsProvider.SaveSettings();
    }

    public Directory[] Directories(int userId)
    {
        return PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).GetDirectories();
    }

    public void UseDevice(int userId, MediaDevice server)
    {
        PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).UseServer(server);
    }

    public MediaDevice[] AvailableDevices(int userId)
    {
        return PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).GetPlexServers().ToArray();
    }

    public MediaDevice CurrentDevice(int userId)
    {
        return PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).ServerCache;
    }
}
