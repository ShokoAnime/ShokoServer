using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NHibernate;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Directory = Shoko.Models.PlexAndKodi.Directory;
using Stream = Shoko.Models.PlexAndKodi.Stream;

namespace Shoko.Server.PlexAndKodi
{
    public static class Helper
    {
        public static string ConstructVideoLocalStream(this IProvider prov, int userid, int vid, string name,
            bool autowatch)
        {
            return prov.ServerUrl(ServerSettings.Instance.ServerPort,
                "Stream/" + vid + "/" + userid + "/" + autowatch + "/" + name, prov.IsExternalRequest());
        }

        public static string ConstructFileStream(this IProvider prov, int userid, string file, bool autowatch)
        {
            return prov.ServerUrl(ServerSettings.Instance.ServerPort,
                "Stream/Filename/" + Base64EncodeUrl(file) + "/" + userid + "/" + autowatch, prov.IsExternalRequest());
        }

        public static string ConstructImageLink(this IProvider prov, int type, int id)
        {
            return prov.ServerUrl(ServerSettings.Instance.ServerPort,
                ShokoServer.PathAddressREST + "/" + type + "/" + id);
        }

        public static string ConstructSupportImageLink(this IProvider prov, string name)
        {
            string relation = prov.GetRelation().ToString(CultureInfo.InvariantCulture);
            return prov.ServerUrl(ServerSettings.Instance.ServerPort,
                ShokoServer.PathAddressREST + "/Support/" + name + "/" + relation);
        }

        public static string ConstructSupportImageLinkTV(this IProvider prov, string name)
        {
            return prov.ServerUrl(ServerSettings.Instance.ServerPort,
                ShokoServer.PathAddressREST + "/Support/" + name);
        }

        public static string ConstructThumbLink(this IProvider prov, int type, int id)
        {
            string relation = prov.GetRelation().ToString(CultureInfo.InvariantCulture);
            return prov.ServerUrl(ServerSettings.Instance.ServerPort,
                ShokoServer.PathAddressREST + "/Thumb/" + type + "/" + id + "/" + relation);
        }

        public static string ConstructTVThumbLink(this IProvider prov, int type, int id)
        {
            return prov.ServerUrl(ServerSettings.Instance.ServerPort,
                ShokoServer.PathAddressREST + "/Thumb/" + type + "/" + id + "/1.3333");
        }

        public static string ConstructCharacterImage(this IProvider prov, int id)
        {
            return prov.ServerUrl(ServerSettings.Instance.ServerPort, ShokoServer.PathAddressREST + "/2/" + id);
        }

        public static string ConstructSeiyuuImage(this IProvider prov, int id)
        {
            return prov.ServerUrl(ServerSettings.Instance.ServerPort, ShokoServer.PathAddressREST + "/3/" + id);
        }

        public static readonly Lazy<Dictionary<string, double>> _relations =
            new Lazy<Dictionary<string, double>>(CreateRelationsMap, isThreadSafe: true);

        private static double GetRelation(this IProvider prov)
        {
            var relations = _relations.Value;

            string product = prov.RequestHeader("X-Plex-Product");
            if (product != null)
            {
                string kh = product.ToUpper();
                foreach (string n in relations.Keys.Where(a => a != "DEFAULT"))
                {
                    if (n != null && kh.Contains(n))
                        return relations[n];
                }
            }
            return relations["DEFAULT"];
        }

        private static Dictionary<string, double> CreateRelationsMap()
        {
            var relations = new Dictionary<string, double>();
            string[] aspects = ServerSettings.Instance.Plex.ThumbnailAspects.Split(',');

            for (int x = 0; x < aspects.Length; x += 2)
            {
                string key = aspects[x].Trim().ToUpper();

                Double.TryParse(aspects[x + 1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val);
                relations.Add(key, val);
            }

            if (!relations.ContainsKey("DEFAULT"))
            {
                relations.Add("DEFAULT", 0.666667D);
            }

            return relations;
        }

        public static string Base64EncodeUrl(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes).Replace("+", "-").Replace("/", "_").Replace("=", ",");
        }

        public static string Base64DecodeUrl(string url)
        {
            byte[] data = Convert.FromBase64String(url.Replace("-", "+").Replace("_", "/").Replace(",", "="));
            return Encoding.UTF8.GetString(data);
        }

        public static SVR_JMMUser GetUser(string userid)
        {
            IReadOnlyList<SVR_JMMUser> allusers = RepoFactory.JMMUser.GetAll();
            foreach (SVR_JMMUser n in allusers)
            {
                if (userid.FindIn(n.GetPlexUsers()))
                {
                    return n;
                }
            }
            return allusers.FirstOrDefault(a => a.IsAdmin == 1) ??
                   allusers.FirstOrDefault(a => a.Username == "Default") ?? allusers.First();
        }

        public static SVR_JMMUser GetJMMUser(string userid)
        {
            IReadOnlyList<SVR_JMMUser> allusers = RepoFactory.JMMUser.GetAll();
            Int32.TryParse(userid, out int id);
            return allusers.FirstOrDefault(a => a.JMMUserID == id) ??
                   allusers.FirstOrDefault(a => a.IsAdmin == 1) ??
                   allusers.FirstOrDefault(a => a.Username == "Default") ?? allusers.First();
        }


        public static void AddLinksToAnimeEpisodeVideo(IProvider prov, Video v, int userid)
        {
            if (v.AnimeType == AnimeTypes.AnimeEpisode.ToString())
                v.Key = prov.ContructVideoUrl(userid, v.Id, JMMType.Episode);
            else if (v.Medias != null && v.Medias.Count > 0)
                v.Key = prov.ContructVideoUrl(userid, v.Medias[0].Id, JMMType.File);
            if (v.Medias == null) return;
            foreach (Media m in v.Medias)
            {
                if (m?.Parts == null) continue;
                foreach (Part p in m.Parts)
                {
                    string ff = "file." + p.Container;
                    p.Key = prov.ConstructVideoLocalStream(userid, m.Id, ff, prov.AutoWatch);
                    if (p.Streams == null) continue;
                    foreach (Stream s in p.Streams.Where(a => a.File != null && a.StreamType == 3))
                    {
                        s.Key = prov.ConstructFileStream(userid, s.File, prov.AutoWatch);
                    }
                }
            }
        }

        public static Video VideoFromVideoLocal(IProvider prov, SVR_VideoLocal v, int userid)
        {
            Video l = new Video
            {
                AnimeType = AnimeTypes.AnimeFile.ToString(),
                Id = v.VideoLocalID,
                Type = "episode",
                Summary = "Episode Overview Not Available", //TODO Internationalization
                Title = Path.GetFileNameWithoutExtension(v.FileName),
                AddedAt = v.DateTimeCreated.ToUnixTime(),
                UpdatedAt = v.DateTimeUpdated.ToUnixTime(),
                OriginallyAvailableAt = v.DateTimeCreated.ToPlexDate(),
                Year = v.DateTimeCreated.Year,
                Medias = new List<Media>()
            };
            SVR_VideoLocal_User vlr = v.GetUserRecord(userid);
            if (vlr?.WatchedDate != null)
                l.LastViewedAt = vlr.WatchedDate.Value.ToUnixTime();
            if (vlr?.ResumePosition > 0)
                l.ViewOffset = vlr.ResumePosition;
            if (v.Media != null)
            {
                Media m = new Media(v.VideoLocalID, v.Media);
                l.Medias.Add(m);
                l.Duration = m.Duration;
            }

            AddLinksToAnimeEpisodeVideo(prov, l, userid);
            return l;
        }


        public static Video VideoFromAnimeEpisode(IProvider prov, List<CrossRef_AniDB_TvDBV2> cross,
            KeyValuePair<SVR_AnimeEpisode, CL_AnimeEpisode_User> e, int userid)
        {
            Video v = GenerateVideoFromAnimeEpisode(e.Key, e.Value.JMMUserID);
            if (v?.Thumb != null)
                v.Thumb = prov.ReplaceSchemeHost(v.Thumb);
            if (v != null)
            {
                if (e.Key.AniDB_Episode == null) return v;
                if (e.Value != null)
                {
                    v.ViewCount = e.Value.WatchedCount;
                    if (e.Value.WatchedDate.HasValue)
                        v.LastViewedAt = e.Value.WatchedDate.Value.ToUnixTime();
                }
                v.ParentIndex = 1;
                if (e.Key.EpisodeTypeEnum != EpisodeType.Episode)
                {
                    v.ParentIndex = 0;
                }

                if (e.Key.EpisodeTypeEnum == EpisodeType.Episode)
                {
                    string client = prov.GetPlexClient().Product;
                    if (client == "Plex for Windows" || client == "Plex Home Theater")
                        v.Title = $"{v.EpisodeNumber}. {v.Title}";
                }

                if (cross != null && cross.Count > 0)
                {
                    CrossRef_AniDB_TvDBV2 c2 =
                        cross.FirstOrDefault(
                            a =>
                                a.AniDBStartEpisodeType == v.EpisodeType &&
                                a.AniDBStartEpisodeNumber <= v.EpisodeNumber);
                    if (c2?.TvDBSeasonNumber > 0)
                        v.ParentIndex = c2.TvDBSeasonNumber;
                }
                AddLinksToAnimeEpisodeVideo(prov, v, userid);
            }
            v.AddResumePosition(prov, userid);

            return v;
        }

        private static readonly Regex UrlSafe = new Regex("[ \\$^`:<>\\[\\]\\{\\}\"“\\+%@/;=\\?\\\\\\^\\|~‘,]",
            RegexOptions.Compiled);

        private static readonly Regex UrlSafe2 = new Regex("[^0-9a-zA-Z_\\.\\s]", RegexOptions.Compiled);

        public static Video GenerateVideoFromAnimeEpisode(SVR_AnimeEpisode ep, int userID)
        {
            Video l = new Video();
            List<SVR_VideoLocal> vids = ep.GetVideoLocals();
            l.Type = "episode";
            l.Summary = "Episode Overview Not Available"; //TODO Intenationalization
            l.Id = ep.AnimeEpisodeID;
            l.AnimeType = AnimeTypes.AnimeEpisode.ToString();
            if (vids.Count > 0)
            {
                //List<string> hashes = vids.Select(a => a.Hash).Distinct().ToList();
                l.Title = Path.GetFileNameWithoutExtension(vids[0].FileName);
                l.AddedAt = vids[0].DateTimeCreated.ToUnixTime();
                l.UpdatedAt = vids[0].DateTimeUpdated.ToUnixTime();
                l.OriginallyAvailableAt = vids[0].DateTimeCreated.ToPlexDate();
                l.Year = vids[0].DateTimeCreated.Year;
                l.Medias = new List<Media>();
                foreach (SVR_VideoLocal v in vids)
                {
                    if (v?.Media == null) continue;
                    var legacy = new Media(v.VideoLocalID, v.Media);
                    var place = v.GetBestVideoLocalPlace();
                    legacy.Parts.ForEach(p =>
                        {
                            if (string.IsNullOrEmpty(p.LocalKey))
                                p.LocalKey = place.FullServerPath;
                            string name = UrlSafe.Replace(Path.GetFileName(place.FilePath), " ").CompactWhitespaces()
                                .Trim();
                            name = UrlSafe2.Replace(name, string.Empty)
                                .Trim()
                                .CompactCharacters('.')
                                .Replace(" ", "_")
                                .CompactCharacters('_')
                                .Replace("_.", ".");
                            while (name.StartsWith("_"))
                                name = name.Substring(1);
                            while (name.StartsWith("."))
                                name = name.Substring(1);
                            p.Key = ((IProvider) null).ReplaceSchemeHost(
                                ((IProvider) null).ConstructVideoLocalStream(userID, v.VideoLocalID, name, false));
                            if (p.Streams == null) return;
                            foreach (Stream s in p.Streams.Where(a => a.File != null && a.StreamType == 3).ToList())
                                s.Key =
                                    ((IProvider) null).ReplaceSchemeHost(
                                        ((IProvider) null).ConstructFileStream(userID, s.File, false));
                        });
                    l.Medias.Add(legacy);
                }

                string title = ep.Title;
                if (!String.IsNullOrEmpty(title)) l.Title = title;

                string romaji = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.AniDB_EpisodeID, Shoko.Plugin.Abstractions.DataModels.TitleLanguage.Romaji)
                    .FirstOrDefault()?.Title;
                if (!String.IsNullOrEmpty(romaji)) l.OriginalTitle = romaji;

                AniDB_Episode aep = ep?.AniDB_Episode;
                if (aep != null)
                {
                    l.EpisodeNumber = aep.EpisodeNumber;
                    l.Index = aep.EpisodeNumber;
                    l.EpisodeType = aep.EpisodeType;
                    l.Rating = (int) Single.Parse(aep.Rating, CultureInfo.InvariantCulture);
                    AniDB_Vote vote =
                        RepoFactory.AniDB_Vote.GetByEntityAndType(ep.AnimeEpisodeID, AniDBVoteType.Episode);
                    if (vote != null) l.UserRating = (int) (vote.VoteValue / 100D);

                    if (aep.GetAirDateAsDate().HasValue)
                    {
                        l.Year = aep.GetAirDateAsDate()?.Year ?? 0;
                        l.OriginallyAvailableAt = aep.GetAirDateAsDate()?.ToPlexDate();
                    }

                    #region TvDB

                    TvDB_Episode tvep = ep.TvDBEpisode;
                    if (tvep != null)
                    {
                        l.Thumb = tvep.GenPoster(null);
                        l.Summary = tvep.Overview;
                        l.Season = $"{tvep.SeasonNumber}x{tvep.EpisodeNumber:0#}";
                    }
                    #endregion
                }
                if (l.Thumb == null || l.Summary == null)
                {
                    l.Thumb = ((IProvider) null).ConstructSupportImageLink("plex_404.png");
                    l.Summary = "Episode Overview not Available";
                }
            }
            l.Id = ep.AnimeEpisodeID;
            return l;
        }

        private static void GetValidVideoRecursive(IProvider prov, SVR_GroupFilter f, int userid, Directory pp)
        {
            List<SVR_GroupFilter> gfs = RepoFactory.GroupFilter.GetByParentID(f.GroupFilterID)
                .Where(a => a.GroupsIds.ContainsKey(userid) && a.GroupsIds[userid].Count > 0)
                .ToList();

            foreach (SVR_GroupFilter gg in gfs.Where(a => (a.FilterType & (int) GroupFilterType.Directory) == 0))
            {
                if (gg.GroupsIds.ContainsKey(userid))
                {
                    HashSet<int> groups = gg.GroupsIds[userid];
                    if (groups.Count != 0)
                    {
                        foreach (int grp in groups.Randomize(f.GroupFilterID))
                        {
                            SVR_AnimeGroup ag = RepoFactory.AnimeGroup.GetByID(grp);
                            Video v = ag.GetPlexContract(userid);
                            if (v?.Art == null || v.Thumb == null) continue;
                            pp.Art = prov.ReplaceSchemeHost(v.Art);
                            pp.Thumb = prov.ReplaceSchemeHost(v.Thumb);
                            break;
                        }
                    }
                }
                if (pp.Art != null)
                    break;
            }
            if (pp.Art == null)
            {
                foreach (SVR_GroupFilter gg in gfs
                    .Where(a => (a.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory &&
                                a.InvisibleInClients == 0)
                    .Randomize(f.GroupFilterID))
                {
                    GetValidVideoRecursive(prov, gg, userid, pp);
                    if (pp.Art != null)
                        break;
                }
            }
            pp.LeafCount = gfs.Count;
            pp.ViewedLeafCount = 0;
        }

        public static Directory DirectoryFromFilter(IProvider prov, SVR_GroupFilter gg,
            int userid)
        {
            Directory pp = new Directory {Type = "show"};
            pp.Key = prov.ConstructFilterIdUrl(userid, gg.GroupFilterID);
            pp.Title = gg.GroupFilterName;
            pp.Id = gg.GroupFilterID;
            pp.AnimeType = AnimeTypes.AnimeGroupFilter.ToString();
            if ((gg.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory)
            {
                GetValidVideoRecursive(prov, gg, userid, pp);
            }
            else if (gg.GroupsIds.ContainsKey(userid))
            {
                HashSet<int> groups = gg.GroupsIds[userid];
                if (groups.Count == 0) return pp;
                pp.LeafCount = groups.Count;
                pp.ViewedLeafCount = 0;
                foreach (int grp in groups.Randomize())
                {
                    SVR_AnimeGroup ag = RepoFactory.AnimeGroup.GetByID(grp);
                    Video v = ag.GetPlexContract(userid);
                    if (v?.Art == null || v.Thumb == null) continue;
                    pp.Art = prov.ReplaceSchemeHost(v.Art);
                    pp.Thumb = prov.ReplaceSchemeHost(v.Thumb);
                    break;
                }
                return pp;
            }
            return pp;
        }


        public static void AddInformationFromMasterSeries(Video v, CL_AnimeSeries_User cserie, Video nv,
            bool omitExtraData = false)
        {
            bool ret = false;
            v.ParentThumb = v.GrandparentThumb = nv.Thumb;
            if (cserie.AniDBAnime.AniDBAnime.Restricted > 0)
                v.ContentRating = "R";
            switch (cserie.AniDBAnime.AniDBAnime.AnimeType)
            {
                case (int) AnimeType.Movie:
                    v.Type = "movie";
                    if (v.Title.StartsWith("Complete Movie"))
                    {
                        v.Title = nv.Title;
                        v.Summary = nv.Summary;
                        v.Index = 0;
                        ret = true;
                    }
                    else if (v.Title.StartsWith("Part "))
                    {
                        v.Title = nv.Title + " - " + v.Title;
                        v.Summary = nv.Summary;
                    }
                    v.Thumb = nv.Thumb;
                    break;
                case (int) AnimeType.OVA:
                    if (v.Title == "OVA")
                    {
                        v.Title = nv.Title;
                        v.Type = "movie";
                        v.Thumb = nv.Thumb;
                        v.Summary = nv.Summary;
                        v.Index = 0;
                        ret = true;
                    }
                    break;
            }
            if (String.IsNullOrEmpty(v.Art))
                v.Art = nv.Art;
            if (!omitExtraData)
            {
                if (v.Tags == null)
                    v.Tags = nv.Tags;
                if (v.Genres == null)
                    v.Genres = nv.Genres;
                if (v.Roles == null)
                    v.Roles = nv.Roles;
            }
            if (v.Rating == 0)
                v.Rating = nv.Rating;
            if (v.Thumb == null)
                v.Thumb = v.ParentThumb;
            v.IsMovie = ret;
        }

        public static IEnumerable<T> Randomize<T>(this IEnumerable<T> source, int seed = -1)
        {
            var rnd = seed == -1 ? new Random() : new Random(seed);
            return source.OrderBy(item => rnd.Next());
        }

        public static string GetRandomFanartFromVideo(Video v, IProvider prov)
        {
            return GetRandomArtFromList(v.Fanarts, prov);
        }

        public static string GetRandomBannerFromVideo(Video v, IProvider prov)
        {
            return GetRandomArtFromList(v.Banners, prov);
        }

        public static string GetRandomArtFromList(List<Contract_ImageDetails> list, IProvider prov)
        {
            if (list == null || list.Count == 0) return null;
            Contract_ImageDetails art;
            if (list.Count == 1)
            {
                art = list[0];
            }
            else
            {
                Random rand = new Random();
                art = list[rand.Next(0, list.Count)];
            }
            ImageDetails details = new ImageDetails
            {
                ImageID = art.ImageID,
                ImageType = (ImageEntityType) art.ImageType
            };
            return details.GenArt(prov);
        }

        public static Video GenerateFromAnimeGroup(SVR_AnimeGroup grp, int userid, List<SVR_AnimeSeries> allSeries,
            ISessionWrapper session = null)
        {
            CL_AnimeGroup_User cgrp = grp.GetUserContract(userid);
            int subgrpcnt = grp.GetAllChildGroups().Count;

            if ((cgrp.Stat_SeriesCount == 1) && (subgrpcnt == 0))
            {
                SVR_AnimeSeries ser = ShokoServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                CL_AnimeSeries_User cserie = ser?.GetUserContract(userid);
                if (cserie == null) return null;
                Video v = GenerateFromSeries(cserie, ser, ser.GetAnime(), userid, session);
                v.AirDate = ser.AirDate;
                v.UpdatedAt = ser.LatestEpisodeAirDate.HasValue
                    ? ser.LatestEpisodeAirDate.Value.ToUnixTime()
                    : null;
                v.Group = cgrp;
                return v;
            }
            else
            {
                SVR_AnimeSeries ser = grp.DefaultAnimeSeriesID.HasValue
                    ? allSeries.FirstOrDefault(a => a.AnimeSeriesID == grp.DefaultAnimeSeriesID.Value)
                    : allSeries.Find(a => a.AirDate != DateTime.MinValue);
                if (ser == null && allSeries.Count > 0)
                    ser = allSeries[0];
                CL_AnimeSeries_User cserie = ser?.GetUserContract(userid);
                Video v = FromGroup(cgrp, cserie, userid, subgrpcnt);
                v.Group = cgrp;
                v.AirDate = cgrp.Stat_AirDate_Min ?? DateTime.MinValue;
                v.UpdatedAt = cgrp.LatestEpisodeAirDate?.ToUnixTime();
                v.Rating = (int) Math.Round((grp.AniDBRating / 100), 1);
                List<Tag> newTags = new List<Tag>();
                foreach (AniDB_Tag tag in grp.Tags)
                {
                    Tag newTag = new Tag();
                    TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                    newTag.Value = textInfo.ToTitleCase(tag.TagName.Trim());
                    if (!newTags.Contains(newTag)) newTags.Add(newTag);
                }
                v.Genres = newTags;
                if (ser == null) return v;
                List<AnimeTitle> newTitles = ser.GetAnime()
                    .GetTitles()
                    .Select(title => new AnimeTitle
                    {
                        Title = title.Title,
                        Language = title.LanguageCode,
                        Type = title.TitleType.ToString().ToLower(),
                    })
                    .ToList();
                v.Titles = newTitles;

                v.Roles = new List<RoleTag>();

                //TODO Character implementation is limited in JMM, One Character, could have more than one Seiyuu
                if (ser.GetAnime()?.Contract?.AniDBAnime?.Characters != null)
                {
                    foreach (CL_AniDB_Character c in ser.GetAnime().Contract.AniDBAnime.Characters)
                    {
                        string ch = c?.CharName;
                        AniDB_Seiyuu seiyuu = c?.Seiyuu;
                        if (String.IsNullOrEmpty(ch)) continue;
                        RoleTag t = new RoleTag
                        {
                            Value = seiyuu?.SeiyuuName
                        };
                        if (seiyuu != null)
                            t.TagPicture = ConstructSeiyuuImage(null, seiyuu.AniDB_SeiyuuID);
                        t.Role = ch;
                        t.RoleDescription = c?.CharDescription;
                        t.RolePicture = ConstructCharacterImage(null, c.CharID);
                        v.Roles.Add(t);
                    }
                }
                if (cserie?.AniDBAnime?.AniDBAnime?.Fanarts != null)
                {
                    v.Fanarts = new List<Contract_ImageDetails>();
                    cserie?.AniDBAnime?.AniDBAnime?.Fanarts.ForEach(
                        a =>
                            v.Fanarts.Add(new Contract_ImageDetails
                            {
                                ImageID = a.AniDB_Anime_DefaultImageID,
                                ImageType = a.ImageType
                            }));
                }
                if (cserie?.AniDBAnime?.AniDBAnime?.Banners == null) return v;
                v.Banners = new List<Contract_ImageDetails>();
                cserie?.AniDBAnime?.AniDBAnime?.Banners.ForEach(
                    a =>
                        v.Banners.Add(new Contract_ImageDetails
                        {
                            ImageID = a.AniDB_Anime_DefaultImageID,
                            ImageType = a.ImageType
                        }));
                return v;
            }
        }


        public static List<Video> ConvertToDirectory(List<Video> n, IProvider prov)
        {
            List<Video> ks = new List<Video>();
            foreach (Video n1 in n)
            {
                Video m;
                if (n1 is Directory)
                    m = n1;
                else
                    m = n1.Clone<Directory>(prov);
                m.ParentThumb = m.GrandparentThumb = null;
                ks.Add(m);
            }
            return ks;
        }

        public static Video MayReplaceVideo(Video v1, SVR_AnimeSeries ser, CL_AnimeSeries_User cserie, int userid,
            bool all = true, Video serie = null)
        {
            int epcount = all
                ? ser.GetAnimeEpisodesCountWithVideoLocal()
                : ser.GetAnimeEpisodesNormalCountWithVideoLocal();
            if ((epcount != 1) || (cserie.AniDBAnime.AniDBAnime.AnimeType != (int) AnimeType.OVA &&
                                   cserie.AniDBAnime.AniDBAnime.AnimeType != (int) AnimeType.Movie)) return v1;
            try
            {
                List<SVR_AnimeEpisode> episodes = ser.GetAnimeEpisodes();
                Video v2 = GenerateVideoFromAnimeEpisode(episodes[0], userid);
                if (v2.IsMovie)
                {
                    AddInformationFromMasterSeries(v2, cserie, serie ?? v1);
                    v2.Thumb = (serie ?? v1).Thumb;
                    return v2;
                }
            }
            catch
            {
                //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
            }
            return v1;
        }


        private static Video FromGroup(CL_AnimeGroup_User grp, CL_AnimeSeries_User ser, int userid, int subgrpcnt)
        {
            Directory p = new Directory
            {
                Id = grp.AnimeGroupID,
                AnimeType = AnimeTypes.AnimeGroup.ToString(),
                Title = grp.GroupName,
                Summary = grp.Description,
                Type = "show",
                AirDate = grp.Stat_AirDate_Min ?? DateTime.MinValue
            };
            if (grp.Stat_AllYears.Count > 0)
            {
                p.Year = grp.Stat_AllYears?.Min() ?? 0;
            }
            if (ser != null)
            {
                p.Thumb = ser.AniDBAnime?.AniDBAnime.DefaultImagePoster.GenPoster(null);
                p.Art = ser.AniDBAnime?.AniDBAnime.DefaultImageFanart.GenArt(null);
            }
            p.LeafCount = grp.UnwatchedEpisodeCount + grp.WatchedEpisodeCount;
            p.ViewedLeafCount = grp.WatchedEpisodeCount;
            p.ChildCount = grp.Stat_SeriesCount + subgrpcnt;
            if ((grp.UnwatchedEpisodeCount == 0) && grp.WatchedDate.HasValue)
                p.LastViewedAt = grp.WatchedDate.Value.ToUnixTime();
            return p;
        }

        public static Video GenerateFromSeries(CL_AnimeSeries_User cserie, SVR_AnimeSeries ser, SVR_AniDB_Anime anidb,
            int userid, ISessionWrapper session = null)
        {
            Video v = new Directory();
            Dictionary<SVR_AnimeEpisode, CL_AnimeEpisode_User> episodes = ser.GetAnimeEpisodes()
                .ToDictionary(a => a, a => a.GetUserContract(userid, session));
            episodes = episodes.Where(a => a.Value == null || a.Value.LocalFileCount > 0)
                .ToDictionary(a => a.Key, a => a.Value);
            FillSerie(v, ser, episodes, anidb, cserie, userid);
            if (ser.GetAnimeNumberOfEpisodeTypes() > 1)
                v.Type = "show";
            else if ((cserie.AniDBAnime.AniDBAnime.AnimeType == (int) AnimeType.Movie) ||
                     (cserie.AniDBAnime.AniDBAnime.AnimeType == (int) AnimeType.OVA))
            {
                v = MayReplaceVideo(v, ser, cserie, userid);
            }
            return v;
        }

        private static string SummaryFromAnimeContract(CL_AnimeSeries_User c)
        {
            string s = c.AniDBAnime.AniDBAnime.Description;
            if (String.IsNullOrEmpty(s) && c.MovieDB_Movie != null)
                s = c.MovieDB_Movie.Overview;
            if (String.IsNullOrEmpty(s) && c.TvDB_Series != null && c.TvDB_Series.Count > 0)
                s = c.TvDB_Series[0].Overview;
            return s;
        }


        private static void FillSerie(Video p, SVR_AnimeSeries aser,
            Dictionary<SVR_AnimeEpisode, CL_AnimeEpisode_User> eps,
            SVR_AniDB_Anime anidb, CL_AnimeSeries_User ser, int userid)
        {
            using (ISession session = DatabaseFactory.SessionFactory.OpenSession())
            {
                ISessionWrapper sessionWrapper = session.Wrap();
                CL_AniDB_Anime anime = ser.AniDBAnime.AniDBAnime;
                p.Id = ser.AnimeSeriesID;
                p.AnimeType = AnimeTypes.AnimeSerie.ToString();
                if (ser.AniDBAnime.AniDBAnime.Restricted > 0)
                    p.ContentRating = "R";
                p.Title = aser.GetSeriesName();
                p.Summary = SummaryFromAnimeContract(ser);
                p.Type = "show";
                p.AirDate = DateTime.MinValue;
                TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                if (anime.GetAllTags().Count > 0)
                {
                    p.Genres = new List<Tag>();
                    anime.GetAllTags()
                        .ToList()
                        .ForEach(a => p.Genres.Add(new Tag {Value = textInfo.ToTitleCase(a.Trim())}));
                }
                //p.OriginalTitle
                if (anime.AirDate.HasValue)
                {
                    p.AirDate = anime.AirDate.Value;
                    p.OriginallyAvailableAt = anime.AirDate.Value.ToPlexDate();
                    p.Year = anime.AirDate.Value.Year;
                }
                p.LeafCount = anime.EpisodeCount;
                //p.ChildCount = p.LeafCount;
                p.ViewedLeafCount = ser.WatchedEpisodeCount;
                p.Rating = (int) Math.Round((anime.Rating / 100D), 1);
                AniDB_Vote vote = RepoFactory.AniDB_Vote.GetByEntityAndType(anidb.AnimeID, AniDBVoteType.Anime) ??
                                  RepoFactory.AniDB_Vote.GetByEntityAndType(anidb.AnimeID, AniDBVoteType.AnimeTemp);
                if (vote != null) p.UserRating = (int) (vote.VoteValue / 100D);

                List<CrossRef_AniDB_TvDBV2> ls = ser.CrossRefAniDBTvDBV2;
                if (ls != null && ls.Count > 0)
                {
                    foreach (CrossRef_AniDB_TvDBV2 c in ls)
                    {
                        if (c.TvDBSeasonNumber == 0) continue;
                        p.Season = c.TvDBSeasonNumber.ToString();
                        p.Index = c.TvDBSeasonNumber;
                    }
                }
                p.Thumb = p.ParentThumb = anime.DefaultImagePoster.GenPoster(null);
                p.Art = anime?.DefaultImageFanart?.GenArt(null);
                if (anime?.Fanarts != null)
                {
                    p.Fanarts = new List<Contract_ImageDetails>();
                    anime.Fanarts.ForEach(
                        a =>
                            p.Fanarts.Add(new Contract_ImageDetails
                            {
                                ImageID = a.AniDB_Anime_DefaultImageID,
                                ImageType = a.ImageType
                            }));
                }
                if (anime?.Banners != null)
                {
                    p.Banners = new List<Contract_ImageDetails>();
                    anime.Banners.ForEach(
                        a =>
                            p.Banners.Add(new Contract_ImageDetails
                            {
                                ImageID = a.AniDB_Anime_DefaultImageID,
                                ImageType = a.ImageType
                            }));
                }

                if (eps != null)
                {
                    List<EpisodeType> types = eps.Keys.Where(a => a.AniDB_Episode != null)
                        .Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    p.ChildCount = types.Count > 1 ? types.Count : eps.Keys.Count;
                }
                p.Roles = new List<RoleTag>();

                //TODO Character implementation is limited in JMM, One Character, could have more than one Seiyuu
                if (anime.Characters != null)
                {
                    foreach (CL_AniDB_Character c in anime.Characters)
                    {
                        string ch = c?.CharName;
                        AniDB_Seiyuu seiyuu = c?.Seiyuu;
                        if (String.IsNullOrEmpty(ch)) continue;
                        RoleTag t = new RoleTag
                        {
                            Value = seiyuu?.SeiyuuName
                        };
                        if (seiyuu != null)
                            t.TagPicture = ConstructSeiyuuImage(null, seiyuu.AniDB_SeiyuuID);
                        t.Role = ch;
                        t.RoleDescription = c?.CharDescription;
                        t.RolePicture = ConstructCharacterImage(null, c.CharID);
                        p.Roles.Add(t);
                    }
                }
                p.Titles = new List<AnimeTitle>();
                foreach (var title in anidb.GetTitles())
                {
                    p.Titles.Add(
                        new AnimeTitle {Language = title.LanguageCode, Title = title.Title, Type = title.TitleType.ToString().ToLower()});
                }
            }
        }
    }
}
