using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JmmConversionTool.Xml;
using JMMModels;
using JMMModels.Childs;
using JMMServer.Repositories;
using Raven.Client;
using System.Configuration;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Configuration;
using JMMModels.ClientExtensions;
using JMMServer;
using AniDB_Character = JMMModels.AniDB_Character;
using AnimeGroup = JMMModels.AnimeGroup;
using CustomTag = JMMModels.CustomTag;
using GroupFilter = JMMModels.GroupFilter;
using GroupFilterBaseCondition = JMMModels.Childs.GroupFilterBaseCondition;
using GroupFilterCondition = JMMModels.Childs.GroupFilterCondition;
using GroupFilterConditionType = JMMModels.Childs.GroupFilterConditionType;
using GroupFilterOperator = JMMModels.Childs.GroupFilterOperator;
using GroupFilterSortDirection = JMMModels.Childs.GroupFilterSortDirection;
using GroupFilterSorting = JMMModels.Childs.GroupFilterSorting;
using JMMUser = JMMModels.JMMUser;
using AniDB_ReleaseGroup = JMMModels.AniDB_ReleaseGroup;
using AniDB_Vote = JMMModels.Childs.AniDB_Vote;
using BookmarkedAnime = JMMModels.BookmarkedAnime;
using CommandRequest = JMMServerModels.DB.CommandRequest;
using CommandRequestPriority = JMMServerModels.DB.Childs.CommandRequestPriority;
using CommandRequestType = JMMServerModels.DB.Childs.CommandRequestType;
using DuplicateFile = JMMServerModels.DB.DuplicateFile;
using FileInfo = JMMModels.Childs.FileInfo;
using FileNameHash = JMMServerModels.DB.FileNameHash;
using ImportFolder = JMMModels.ImportFolder;
using ImportFolderType = JMMModels.Childs.ImportFolderType;
using Language = JMMModels.Language;
using LogMessage = JMMServerModels.DB.LogMessage;
using RenameScript = JMMServerModels.DB.RenameScript;
using ScheduledUpdateType = JMMModels.Childs.ScheduledUpdateType;
using Version = JMMServerModels.DB.Version;
using System.Text.RegularExpressions;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Extensions;
using JMMServerModels.DB;
using NHibernate.Linq;
using AniDB_Type = JMMModels.Childs.AniDB_Type;
using AnimeSerie = JMMModels.AnimeSerie;
using ExtendedUserStats = JMMModels.Childs.ExtendedUserStats;
using HashSource = JMMModels.Childs.HashSource;

namespace JmmConversionTool
{
    public static class DBConversion
    {
        static List<GroupFilter> _groupfilter=new List<GroupFilter>();
        static Dictionary<int, JMMModels.AniDB_Tag> _tags=new Dictionary<int, JMMModels.AniDB_Tag>();
        static Dictionary<string, JMMModels.CustomTag> _CustomTags=new Dictionary<string, CustomTag>(); 
        static List<JMMUser> _users=new List<JMMUser>(); 
        static Dictionary<int, AniDB_Creator> _creators=new Dictionary<int, AniDB_Creator>(); 
        static Dictionary<int, AniDB_Character> _chars=new Dictionary<int, AniDB_Character>(); 
        static List<AniDB_ReleaseGroup> _rgroups=new List<AniDB_ReleaseGroup>(); 
        static Dictionary<int, Language> _langs=new Dictionary<int,Language>(); 
        static Dictionary<string, VideoInfo> _videos=new Dictionary<string, VideoInfo>(); 
        static Dictionary<string, AnimeSerie> _series=new Dictionary<string, AnimeSerie>(); 
        static Dictionary<string, List<AnimeSerie>> _grpseries=new Dictionary<string, List<AnimeSerie>>(); 
        public delegate void TextHandler(string text);

        public const int CustomTagDelta = 100000;

        public static event TextHandler OnText;

        public static void DoText(string text)
        {
            OnText?.Invoke(text);
        }

        public static bool Init()
        {
            return JMMServer.Databases.DatabaseHelper.InitDB();
        }

        public static void Convert()
        {
            DoText("Converting tags...");
            ConvertTags();
            DoText("Converting users...");
            ConvertUsers();
            DoText("Converting Creators...");
            ConvertAniDBCreators();
            DoText("Converting Characters...");
            ConvertAniDBCharacters();
            DoText("Converting Languages...");
            ConvertLanguages();
            DoText("Converting Release Groups...");
            ConvertReleaseGroups();
            DoText("Converting Bookmarked Animes...");
            ConvertBookmarkedAnime();
            DoText("Converting Command requests...");
            ConvertCommandRequest();
            DoText("Converting Duplicate Files...");
            ConvertDuplicateFiles();
            DoText("Converting FileName Hashes...");
            ConvertFileNameHash();
            DoText("Converting Import Folders...");
            ConvertImportFolder();
            DoText("Converting Log Messages...");
            ConvertImportFolder();
            DoText("Converting Rename Scripts...");
            ConvertImportFolder();
            DoText("Converting Schedule Updates...");
            ConvertScheduleUpdate();
            DoText("Converting Trakt Friends...");
            ConvertScheduleUpdate();
            DoText("Converting version...");
            ConvertVersion();
            DoText("Converting video infos...");
            ConvertVideoInfos();
            DoText("Converting Group Filters...");
            ConvertGroupFilters();
            Store.Populate(Store.GetSession());
            DoText("Converting Series...");
            ConvertAnimeSeries();
            DoText("Converting Groups...");
            ConvertGroups();
            DoText("Verify Users...");
            Store.Users.Values.ForEach(a=>a.Save(Store.GetSession()));
            DoText("Verify Filters...");
            Store.Filters.Values.ForEach(a => a.Save(Store.GetSession()));
            DoText("DONE");

        }

        private static void ProcessImage(string posterpath, IImageInfo destination, ImageType it, ImageSourceTypes ist, bool ie, bool getsize=true)
        {
#if DEBUG
            string appPath = @"\\192.168.1.8\JMM Server\Images";
#else
            string appPath = Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Images");
#endif
            string imagepath = posterpath;
            destination.ImageLocalPath = imagepath.Replace(appPath, string.Empty);
            destination.ImageEnabled = ie;
            destination.ImageType = it;
            destination.ImageSource = ist;
            if (getsize)
            {
                try
                {
                    Size size = ImageHelper.GetDimensions(imagepath);
                    destination.ImageWidth = size.Width;
                    destination.ImageHeight = size.Height;
                }
                catch
                {
                    // ignored
                }
            }
        }


        public static void ConvertAnimeSeries()
        {
            IDocumentSession session = Store.GetSession();


            Regex partmatch = new Regex("part (?<first>\\d.*?) of (?<second>\\d.*)", RegexOptions.Compiled);
            Regex remsymbols = new Regex("[^A-Za-z0-9 ]", RegexOptions.Compiled);
            Regex remmultispace = new Regex("\\s+", RegexOptions.Compiled);



            //AnimeSeries
            IList<JMMServer.Entities.AnimeSeries> ass = GetAll<JMMServer.Entities.AnimeSeries>();
            Dictionary<int, JMMServer.Entities.AniDB_Anime> adas =
                GetAll<JMMServer.Entities.AniDB_Anime>().ToDictionary(a => a.AnimeID, a => a);
            Dictionary<int, List<JMMServer.Entities.IgnoreAnime>> ias =
                GetAll<JMMServer.Entities.IgnoreAnime>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AnimeSeries_User>> asus =
                GetAll<JMMServer.Entities.AnimeSeries_User>()
                    .GroupBy(a => a.AnimeSeriesID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_CustomTag>> crc =
                GetAll<JMMServer.Entities.CrossRef_CustomTag>()
                    .Where(a => a.CrossRefType == 1)
                    .GroupBy(a => a.CrossRefID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AnimeEpisode>> aes =
                GetAll<JMMServer.Entities.AnimeEpisode>()
                    .GroupBy(a => a.AnimeSeriesID)
                    .ToDictionary(a => a.Key, a => a.ToList());


            //AniDB_Anime
            Dictionary<int, List<JMMServer.Entities.AniDB_Vote>> avs =
                GetAll<JMMServer.Entities.AniDB_Vote>()
                    .Where(a => a.VoteType == 1 || a.VoteType == 2)
                    .GroupBy(a => a.EntityID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AniDB_Anime_Character>> aacs =
                GetAll<JMMServer.Entities.AniDB_Anime_Character>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AniDB_Character_Seiyuu>> acss =
                GetAll<JMMServer.Entities.AniDB_Character_Seiyuu>()
                    .GroupBy(a => a.CharID)
                    .ToDictionary(a => a.Key, a => a.ToList());

            Dictionary<int, List<JMMServer.Entities.AniDB_Anime_Relation>> aars =
                GetAll<JMMServer.Entities.AniDB_Anime_Relation>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AniDB_Anime_Review>> aares =
                GetAll<JMMServer.Entities.AniDB_Anime_Review>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, JMMServer.Entities.AniDB_Review> ars =
                GetAll<JMMServer.Entities.AniDB_Review>().ToDictionary(a => a.ReviewID, a => a);
            Dictionary<int, List<JMMServer.Entities.AniDB_Anime_Similar>> aass =
                GetAll<JMMServer.Entities.AniDB_Anime_Similar>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AniDB_Anime_Tag>> aats =
                GetAll<JMMServer.Entities.AniDB_Anime_Tag>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AniDB_Anime_Title>> aatis =
                GetAll<JMMServer.Entities.AniDB_Anime_Title>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AniDB_GroupStatus>> agss =
                GetAll<JMMServer.Entities.AniDB_GroupStatus>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_AniDB_Other>> caos =
                GetAll<JMMServer.Entities.CrossRef_AniDB_Other>()
                    .Where(a => a.CrossRefType == 1)
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_AniDB_MAL>> cams =
                GetAll<JMMServer.Entities.CrossRef_AniDB_MAL>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_AniDB_TvDBV2>> catvs =
                GetAll<JMMServer.Entities.CrossRef_AniDB_TvDBV2>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_AniDB_TraktV2>> catks =
                GetAll<JMMServer.Entities.CrossRef_AniDB_TraktV2>()
                    .GroupBy(a => a.AnimeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, JMMServer.Entities.MovieDB_Movie> mvs =
                GetAll<JMMServer.Entities.MovieDB_Movie>().ToDictionary(a => a.MovieId, a => a);
            Dictionary<int, JMMServer.Entities.TvDB_Series> tss =
                GetAll<JMMServer.Entities.TvDB_Series>().ToDictionary(a => a.SeriesID, a => a);
            Dictionary<string, JMMServer.Entities.Trakt_Show> tkshs =
                GetAll<JMMServer.Entities.Trakt_Show>().ToDictionary(a => a.TraktID, a => a);
            Dictionary<string, List<JMMServer.Entities.Trakt_Season>> tksss =
                GetAll<JMMServer.Entities.Trakt_Season>()
                    .GroupBy(a => a.Trakt_ShowID)
                    .ToDictionary(a => a.Key.ToString(), a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.MovieDB_Fanart>> mfs =
                GetAll<JMMServer.Entities.MovieDB_Fanart>()
                    .GroupBy(a => a.MovieId)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.MovieDB_Poster>> mfp =
                GetAll<JMMServer.Entities.MovieDB_Poster>()
                    .GroupBy(a => a.MovieId)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.TvDB_ImageFanart>> tifs =
                GetAll<JMMServer.Entities.TvDB_ImageFanart>()
                    .GroupBy(a => a.SeriesID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.TvDB_ImagePoster>> tips =
                GetAll<JMMServer.Entities.TvDB_ImagePoster>()
                    .GroupBy(a => a.SeriesID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.TvDB_ImageWideBanner>> tiws =
                GetAll<JMMServer.Entities.TvDB_ImageWideBanner>()
                    .GroupBy(a => a.SeriesID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.Trakt_ImageFanart>> tkifs =
                GetAll<JMMServer.Entities.Trakt_ImageFanart>()
                    .GroupBy(a => a.Trakt_ShowID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.Trakt_ImagePoster>> tkips =
                GetAll<JMMServer.Entities.Trakt_ImagePoster>()
                    .GroupBy(a => a.Trakt_ShowID)
                    .ToDictionary(a => a.Key, a => a.ToList());

            //Anime Episodes
            Dictionary<int, List<JMMServer.Entities.AnimeEpisode_User>> aeus =
                GetAll<JMMServer.Entities.AnimeEpisode_User>()
                    .GroupBy(a => a.AnimeEpisodeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.AniDB_Vote>> aves =
                GetAll<JMMServer.Entities.AniDB_Vote>()
                    .Where(a => a.VoteType == 3)
                    .GroupBy(a => a.EntityID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, JMMServer.Entities.AniDB_Episode> aeps =
                GetAll<JMMServer.Entities.AniDB_Episode>().ToDictionary(a => a.EpisodeID, a => a);
            Dictionary<int, List<JMMServer.Entities.TvDB_Episode>> tves =
                GetAll<JMMServer.Entities.TvDB_Episode>()
                    .GroupBy(a => a.SeriesID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.Trakt_Episode>> tres =
                GetAll<JMMServer.Entities.Trakt_Episode>()
                    .GroupBy(a => a.Trakt_ShowID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_File_Episode>> cfes =
                GetAll<JMMServer.Entities.CrossRef_File_Episode>()
                    .GroupBy(a => a.EpisodeID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<string, JMMServer.Entities.AniDB_File> afs =
                GetAll<JMMServer.Entities.AniDB_File>().ToDictionary(a => a.Hash, a => a);
            Dictionary<string, JMMServer.Entities.VideoLocal> vls =
                GetAll<JMMServer.Entities.VideoLocal>().ToDictionary(a => a.Hash, a => a);

            //AniDB_File
            Dictionary<int, List<JMMServer.Entities.CrossRef_Languages_AniDB_File>> clafs =
                GetAll<JMMServer.Entities.CrossRef_Languages_AniDB_File>()
                    .GroupBy(a => a.FileID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_Subtitles_AniDB_File>> csafs =
                GetAll<JMMServer.Entities.CrossRef_Subtitles_AniDB_File>()
                    .GroupBy(a => a.FileID)
                    .ToDictionary(a => a.Key, a => a.ToList());

            //Videolocal
            Dictionary<int, List<JMMServer.Entities.VideoLocal_User>> vlus =
                GetAll<JMMServer.Entities.VideoLocal_User>()
                    .GroupBy(a => a.VideoLocalID)
                    .ToDictionary(a => a.Key, a => a.ToList());
            Dictionary<string, List<JMMServer.Entities.FileFfdshowPreset>> ffdps =
                GetAll<JMMServer.Entities.FileFfdshowPreset>()
                    .GroupBy(a => a.Hash)
                    .ToDictionary(a => a.Key, a => a.ToList());


            foreach (JMMServer.Entities.AnimeSeries aser in ass)
            {
                AnimeSerie s = new AnimeSerie();
                s.Id = aser.AnimeSeriesID.ToString();
                s.GroupId = aser.AnimeGroupID.ToString();
                s.DefaultAudioLanguage = aser.DefaultAudioLanguage;
                s.DefaultSubtitleLanguage = aser.DefaultSubtitleLanguage;
                s.EpisodeAddedDate = aser.EpisodeAddedDate;
                s.LatestLocalEpisodeNumber = aser.LatestLocalEpisodeNumber;
                s.SeriesNameOverride = aser.SeriesNameOverride;
                s.MissingEpisodeCount = aser.MissingEpisodeCount;
                s.MissingEpisodeCountGroups = aser.MissingEpisodeCountGroups;
                s.DateTimeUpdated = aser.DateTimeUpdated;
                s.DateTimeCreated = aser.DateTimeCreated;
                s.IgnoreRecommendations = new List<RecomendationIgnore>();
                if (ias.ContainsKey(aser.AnimeSeriesID))
                {
                    foreach (JMMServer.Entities.IgnoreAnime ia in ias[aser.AnimeSeriesID])
                        s.IgnoreRecommendations.Add(new RecomendationIgnore
                        {
                            Ignore = (RecomedationType) ia.IgnoreType,
                            JMMUserId = ia.JMMUserID.ToString()
                        });
                }
                s.UsersStats = new List<ExtendedUserStats>();
                if (asus.ContainsKey(aser.AnimeSeriesID))
                {
                    foreach (JMMServer.Entities.AnimeSeries_User au in asus[aser.AnimeSeriesID])
                        s.UsersStats.Add(new ExtendedUserStats
                        {
                            IsFave = false,
                            JMMUserId = au.JMMUserID.ToString(),
                            PlayedCount = au.PlayedCount,
                            StoppedCount = au.StoppedCount,
                            UnwatchedEpisodeCount = au.UnwatchedEpisodeCount,
                            WatchedCount = au.WatchedCount,
                            WatchedDate = au.WatchedDate,
                            WatchedEpisodeCount = au.WatchedEpisodeCount
                        });
                }
                s.CustomTags = new List<Anime_Custom_Tag>();
                if (crc.ContainsKey(aser.AnimeSeriesID))
                {
                    foreach (JMMServer.Entities.CrossRef_CustomTag cc in crc[aser.AnimeSeriesID])
                        s.CustomTags.Add(new Anime_Custom_Tag { TagId=cc.CustomTagID.ToString(),Name=_CustomTags[cc.CustomTagID.ToString()].Name});
                }
                if (!adas.ContainsKey(aser.AniDB_ID))
                    throw new Exception("Error, AnimeSeries without AniDB (" + aser.AnimeSeriesID + "/" + aser.AniDB_ID +
                                        ")");
                JMMServer.Entities.AniDB_Anime aa = adas[aser.AniDB_ID];
                AniDB_Anime a = new AniDB_Anime();
                s.AniDB_Anime = a;
                a.Id = aa.AnimeID.ToString();
                a.EpisodeCount = aa.EpisodeCount;
                if (aa.AirDate != null)
                    a.AirDate = new AniDB_Date
                    {
                        Date = aa.AirDate.Value.ToUnixTime(),
                        Precision = AniDB_Date_Precision.Day
                    };
                if (aa.EndDate != null)
                    a.EndDate = new AniDB_Date
                    {
                        Date = aa.EndDate.Value.ToUnixTime(),
                        Precision = AniDB_Date_Precision.Day
                    };
                a.Url = aa.URL;
                a.Picname = aa.Picname;
                ProcessImage(aa.PosterPath, a, ImageType.Poster, ImageSourceTypes.AniDB, true);
                a.BeginYear = aa.BeginYear;
                a.EndYear = aa.EndYear;
                a.AnimeType = (AniDB_Type) aa.AnimeType;
                if (a.AnimeType == AniDB_Type.Movie || a.AnimeType == AniDB_Type.OVA)
                    s.IsMovie = true;
                a.MainTitle = aa.MainTitle;
                a.AllTitles = aa.AllTitles;
                a.AllCategories = aa.AllCategories;
                a.AllTags = aa.AllTags;
                a.Description = aa.Description;
                a.EpisodeCountNormal = aa.EpisodeCountNormal;
                a.EpisodeCountSpecial = aa.EpisodeCountSpecial;
                a.Rating = (float) aa.Rating/100F;
                a.VoteCount = aa.VoteCount;
                a.TempRating = (float) aa.TempRating/100F;
                a.TempVoteCount = aa.TempVoteCount;
                a.AvgReviewRating = (float) aa.AvgReviewRating/100F;
                a.ReviewCount = aa.ReviewCount;
                a.DateTimeDescUpdated = aa.DateTimeDescUpdated;
                a.DateTimeUpdated = a.DateTimeCreated = aa.DateTimeUpdated;
                a.AwardList = aa.AwardList;
                a.Restricted = aa.Restricted != 0;
                a.AnimePlanetId = aa.AnimePlanetID;
                a.ANNId = aa.ANNID;
                a.AllCinemaId = aa.AllCinemaID;
                a.AnimeNfo = aa.AnimeNfo;
                a.LatestEpisodeNumber = aa.LatestEpisodeNumber;
                a.DisableExternalLinksFlag = aa.DisableExternalLinksFlag;
                a.MyVotes = new List<AniDB_Vote>();

                if (avs.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_Vote v in avs[aa.AnimeID])
                    {
                        foreach (JMMUser n in _users)
                        {
                            a.MyVotes.Add(new AniDB_Vote
                            {
                                JMMUserId = n.Id,
                                Type = (AniDB_Vote_Type) v.VoteType,
                                Vote = (float) v.VoteValue/100F
                            });
                        }
                    }
                }
                a.MainCreators = new List<AniDB_Creator>();
                a.Creators = new List<AniDB_Creator>();
                a.Characters = new List<AniDB_Anime_Character>();
                if (aacs.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_Anime_Character c in aacs[aa.AnimeID])
                    {
                        if (_chars.ContainsKey(c.CharID))
                        {
                            AniDB_Anime_Character aac = new AniDB_Anime_Character();
                            aac.Creators = new List<CreatorInfo>();
                            ((CharacterInfo) _chars[c.CharID]).CopyTo(aac);
                            if (acss.ContainsKey(c.CharID))
                            {
                                foreach (JMMServer.Entities.AniDB_Character_Seiyuu cs in acss[c.CharID])
                                {
                                    if (_creators.ContainsKey(cs.SeiyuuID))
                                    {
                                        CreatorInfo cinfo = new CreatorInfo();
                                        _creators[cs.SeiyuuID].CopyTo(cinfo);
                                        cinfo.IsMainSeiyuu = true;
                                        if (c.CharType.Contains("main"))
                                            cinfo.ApparenceType = AniDB_Apparence_Type.MainCharacter;
                                        else if (c.CharType.Contains("secondary"))
                                            cinfo.ApparenceType = AniDB_Apparence_Type.SecondaryCast;
                                        else if (c.CharType.Contains("appears"))
                                            cinfo.ApparenceType = AniDB_Apparence_Type.Appears;
                                        else
                                            cinfo.ApparenceType = AniDB_Apparence_Type.Cameo;
                                        aac.Creators.Add(cinfo);
                                    }
                                }
                            }
                            a.Characters.Add(aac);
                        }
                    }
                }
                a.Relations = new List<AniDB_Anime_Relation>();
                if (aars.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_Anime_Relation r in aars[aa.AnimeID])
                    {
                        string v = r.RelationType.ToLower();
                        AniDB_Relation ar;
                        if (v.Contains("sequel"))
                            ar = AniDB_Relation.Sequel;
                        else if (v.Contains("prequel"))
                            ar = AniDB_Relation.Prequel;
                        else if (v.Contains("same"))
                            ar = AniDB_Relation.SameSetting;
                        else if (v.Contains("alternate") && v.Contains("setting"))
                            ar = AniDB_Relation.AlternateSetting;
                        else if (v.Contains("version"))
                            ar = AniDB_Relation.AlternateVersion;
                        else if (v.Contains("music"))
                            ar = AniDB_Relation.MusicVideo;
                        else if (v.Contains("character"))
                            ar = AniDB_Relation.Character;
                        else if (v.Contains("side"))
                            ar = AniDB_Relation.SideStory;
                        else if (v.Contains("parent"))
                            ar = AniDB_Relation.ParentStory;
                        else if (v.Contains("summary"))
                            ar = AniDB_Relation.Summary;
                        else if (v.Contains("full"))
                            ar = AniDB_Relation.FullStory;
                        else
                            ar = AniDB_Relation.Other;
                        a.Relations.Add(new AniDB_Anime_Relation {Id = r.RelatedAnimeID.ToString(), Type = ar});
                    }
                }
                a.Reviews = new List<AniDB_Anime_Review>();
                if (aares.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_Anime_Review r in aares[aa.AnimeID])
                    {
                        if (ars.ContainsKey(r.ReviewID))
                        {
                            JMMServer.Entities.AniDB_Review rr = ars[r.ReviewID];
                            AniDB_Anime_Review rv = new AniDB_Anime_Review();
                            rv.ReviewId = rr.ReviewID;
                            rv.AuthorId = rr.AuthorID;
                            rv.RatingAnimation = rr.RatingAnimation;
                            rv.RatingCharacter = rr.RatingCharacter;
                            rv.RatingEnjoyment = rr.RatingEnjoyment;
                            rv.RatingSound = rr.RatingSound;
                            rv.RatingStory = rr.RatingStory;
                            rv.Text = rr.ReviewText;
                            rv.RatingValue = rr.RatingValue;
                            a.Reviews.Add(rv);
                        }
                    }
                }
                a.Similars = new List<AniDB_Anime_Similar>();
                if (aass.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_Anime_Similar asi in aass[aa.AnimeID])
                        a.Similars.Add(new AniDB_Anime_Similar
                        {
                            Approval = asi.Approval,
                            SimilarId = asi.SimilarAnimeID.ToString(),
                            Total = asi.Total
                        });
                }
                a.Tags = new List<AniDB_Anime_Tag>();
                if (aats.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_Anime_Tag aat in aats[aa.AnimeID])
                    {
                        if (_tags.ContainsKey(aat.TagID))
                            a.Tags.Add(new AniDB_Anime_Tag
                            {
                                Name = _tags[aat.TagID].Name,
                                Approval = aat.Approval,
                                TagId = aat.TagID.ToString(),
                                Weight = aat.Weight
                            });
                    }
                }
                a.Titles = new List<AniDB_Anime_Title>();
                if (aatis.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_Anime_Title t in aatis[aa.AnimeID])
                        a.Titles.Add(new AniDB_Anime_Title
                        {
                            Language = t.Language,
                            Title = t.Title,
                            Type = t.TitleType
                        });
                }
                a.ReleaseGroups = new List<AniDB_Anime_ReleaseGroup>();
                if (agss.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.AniDB_GroupStatus t in agss[aa.AnimeID])
                        a.ReleaseGroups.Add(new AniDB_Anime_ReleaseGroup
                        {
                            Rating = (float) t.Rating/100F,
                            CompletitionState = (AniDB_Completition_State) t.CompletionState,
                            EpisodeRange = t.EpisodeRange,
                            GroupId = t.GroupID.ToString(),
                            GroupName = t.GroupName,
                            LastEpisodeNumber = t.LastEpisodeNumber,
                            Votes = t.Votes
                        });
                }
                a.MovieDBs = new List<AniDB_Anime_MovieDB>();
                a.MovieDBFanarts = new List<MovieDB_Image>();
                a.MovieDBPosters = new List<MovieDB_Image>();
                if (caos.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.CrossRef_AniDB_Other co in caos[aa.AnimeID])
                    {
                        if (mvs.ContainsKey(int.Parse(co.CrossRefID)))
                        {
                            JMMServer.Entities.MovieDB_Movie mov = mvs[int.Parse(co.CrossRefID)];
                            a.MovieDBs.Add(new AniDB_Anime_MovieDB
                            {
                                CrossRefSource = (CrossRefSourceType) co.CrossRefSource,
                                MovieId = mov.MovieId,
                                Name = mov.MovieName,
                                OriginalName = mov.OriginalName,
                                Overview = mov.Overview
                            });
                            if (mfs.ContainsKey(mov.MovieId))
                            {
                                foreach (JMMServer.Entities.MovieDB_Fanart f in mfs[mov.MovieId])
                                {
                                    MovieDB_Image mi = new MovieDB_Image();
                                    mi.URL = f.URL;
                                    mi.ImageWidth = mi.ImageWidth;
                                    mi.ImageHeight = mi.ImageHeight;
                                    ProcessImage(f.FullImagePath, mi, ImageType.Fanart, ImageSourceTypes.MovieDB,
                                        f.Enabled != 0, false);
                                    a.MovieDBFanarts.Add(mi);
                                }
                            }
                            if (mfp.ContainsKey(mov.MovieId))
                            {
                                foreach (JMMServer.Entities.MovieDB_Poster f in mfp[mov.MovieId])
                                {
                                    MovieDB_Image mi = new MovieDB_Image();
                                    mi.URL = f.URL;
                                    mi.ImageWidth = mi.ImageWidth;
                                    mi.ImageHeight = mi.ImageHeight;
                                    ProcessImage(f.FullImagePath, mi, ImageType.Poster, ImageSourceTypes.MovieDB,
                                        f.Enabled != 0, false);
                                    a.MovieDBFanarts.Add(mi);
                                }
                            }
                        }
                    }
                }
                a.MALs = new List<AniDB_Anime_MAL>();
                if (cams.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.CrossRef_AniDB_MAL ma in cams[aa.AnimeID])
                        a.MALs.Add(new AniDB_Anime_MAL
                        {
                            CrossRefSource = (CrossRefSourceType) ma.CrossRefSource,
                            MalId = ma.MALID,
                            StartEpisodeNumber = ma.StartEpisodeNumber,
                            StartEpisodeType = (AniDB_Episode_Type) ma.StartEpisodeType,
                            Title = ma.MALTitle
                        });
                }
                a.TvDBs = new List<AniDB_Anime_TvDB>();
                a.TvDBPosters = new List<TvDB_Image>();
                a.TvDBBanners = new List<TvDB_Image>();
                a.TvDBFanarts = new List<TvDB_Image>();
                if (catvs.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.CrossRef_AniDB_TvDBV2 tv in catvs[aa.AnimeID])
                    {
                        if (tss.ContainsKey(tv.TvDBID))
                        {
                            JMMServer.Entities.TvDB_Series ser = tss[tv.TvDBID];
                            a.TvDBs.Add(new AniDB_Anime_TvDB
                            {
                                Banner = ser.Banner,
                                CrossRefSource = (CrossRefSourceType) tv.CrossRefSource,
                                Fanart = ser.Fanart,
                                LastUpdated = long.Parse(ser.Lastupdated).ToDateTime(),
                                Overview = ser.Overview
                            });
                            if (tifs.ContainsKey(tv.TvDBID))
                            {
                                foreach (JMMServer.Entities.TvDB_ImageFanart f in tifs[tv.TvDBID])
                                {
                                    TvDB_Image ti = new TvDB_Image();
                                    ti.Id = f.Id;
                                    ti.BannerPath = f.BannerPath;
                                    ti.Rating = 0;
                                    ti.RatingCount = 0;
                                    ti.Colors = f.Colors;
                                    ti.Season = null;
                                    ti.IsSeriesName = false;
                                    ti.Language = f.Language;
                                    ti.ThumbnailPath = f.ThumbnailPath;
                                    ti.Chosen = f.Chosen != 0;
                                    ProcessImage(f.FullImagePath, ti, ImageType.Fanart, ImageSourceTypes.TvDB,
                                        f.Enabled != 0);
                                    a.TvDBFanarts.Add(ti);
                                }
                            }
                            if (tips.ContainsKey(tv.TvDBID))
                            {
                                foreach (JMMServer.Entities.TvDB_ImagePoster f in tips[tv.TvDBID])
                                {
                                    TvDB_Image ti = new TvDB_Image();
                                    ti.Id = f.Id;
                                    ti.BannerPath = f.BannerPath;
                                    ti.Rating = 0;
                                    ti.RatingCount = 0;
                                    ti.Season = f.SeasonNumber;
                                    ti.IsSeriesName = false;
                                    ti.Language = f.Language;
                                    ProcessImage(f.FullImagePath, ti, ImageType.Poster, ImageSourceTypes.TvDB,
                                        f.Enabled != 0);
                                    a.TvDBPosters.Add(ti);
                                }
                            }
                            if (tiws.ContainsKey(tv.TvDBID))
                            {
                                foreach (JMMServer.Entities.TvDB_ImageWideBanner f in tiws[tv.TvDBID])
                                {
                                    TvDB_Image ti = new TvDB_Image();
                                    ti.Id = f.Id;
                                    ti.BannerPath = f.BannerPath;
                                    ti.Rating = 0;
                                    ti.RatingCount = 0;
                                    ti.Season = f.SeasonNumber;
                                    ti.IsSeriesName = false;
                                    ti.Language = f.Language;
                                    ProcessImage(f.FullImagePath, ti, ImageType.Banner, ImageSourceTypes.TvDB,
                                        f.Enabled != 0);
                                    a.TvDBBanners.Add(ti);
                                }
                            }
                        }
                    }
                }
                a.Trakts = new List<AniDB_Anime_Trakt>();
                a.TraktFanarts = new List<Trakt_Image>();
                a.TraktPosters = new List<Trakt_Image>();
                if (catks.ContainsKey(aa.AnimeID))
                {
                    foreach (JMMServer.Entities.CrossRef_AniDB_TraktV2 tk in catks[aa.AnimeID])
                    {
                        if (tkshs.ContainsKey(tk.TraktID))
                        {
                            JMMServer.Entities.Trakt_Show tshow = tkshs[tk.TraktID];
                            JMMServer.Entities.Trakt_Season tseas = null;
                            if (tksss.ContainsKey(tk.TraktID))
                                tseas = tksss[tk.TraktID].FirstOrDefault(ca => ca.Season == tk.TraktSeasonNumber);
                            int? year = null;
                            if (!string.IsNullOrEmpty(tshow.Year))
                                year = Int32.Parse(tshow.Year);
                            a.Trakts.Add(new AniDB_Anime_Trakt
                            {
                                CrossRefSource = (CrossRefSourceType) tk.CrossRefSource,
                                Overview = tshow.Overview,
                                ProviderSeasonNumber = tk.TraktSeasonNumber,
                                ProviderStartEpisodeNumber = tk.TraktStartEpisodeNumber,
                                SeasonUrl = tseas?.URL ?? null,
                                StartEpisodeNumber = tk.AniDBStartEpisodeNumber,
                                StartEpisodeType = (AniDB_Episode_Type) tk.AniDBStartEpisodeType,
                                Title = tshow.Title,
                                TraktId = tshow.TraktID,
                                TvDBId = tshow.TvDB_ID,
                                Url = tshow.URL,
                                Year = year
                            });
                            if (tkifs.ContainsKey(int.Parse(tk.TraktID)))
                            {
                                foreach (JMMServer.Entities.Trakt_ImageFanart f in tkifs[int.Parse(tk.TraktID)])
                                {
                                    Trakt_Image ti = new Trakt_Image();
                                    ti.ImageUrl = f.ImageURL;
                                    ti.Season = f.Season;
                                    ti.IsMovie = false;
                                    ProcessImage(f.FullImagePath, ti, ImageType.Fanart, ImageSourceTypes.TraktDB,
                                        f.Enabled != 0);
                                    a.TraktFanarts.Add(ti);
                                }
                            }
                            if (tkips.ContainsKey(int.Parse(tk.TraktID)))
                            {
                                foreach (JMMServer.Entities.Trakt_ImagePoster f in tkips[int.Parse(tk.TraktID)])
                                {
                                    Trakt_Image ti = new Trakt_Image();
                                    ti.ImageUrl = f.ImageURL;
                                    ti.Season = f.Season;
                                    ti.IsMovie = false;
                                    ProcessImage(f.FullImagePath, ti, ImageType.Poster, ImageSourceTypes.TraktDB,
                                        f.Enabled != 0);
                                    a.TraktPosters.Add(ti);
                                }
                            }
                        }
                    }
                }
                s.Episodes = new List<AnimeEpisode>();
                if (aes.ContainsKey(aser.AnimeSeriesID))
                {
                    foreach (JMMServer.Entities.AnimeEpisode ep in aes[aser.AnimeSeriesID])
                    {
                        AnimeEpisode e = new AnimeEpisode();
                        s.Episodes.Add(e);
                        e.AniDbEpisodes = new Dictionary<int, List<AniDB_Episode>>();
                        e.Id = ep.AnimeEpisodeID.ToString();
                        e.UsersStats = new List<UserStats>();
                        e.DateTimeUpdated = ep.DateTimeUpdated;
                        e.DateTimeCreated = ep.DateTimeCreated;
                        if (aeus.ContainsKey(ep.AnimeEpisodeID))
                        {
                            foreach (JMMServer.Entities.AnimeEpisode_User aeu in aeus[ep.AnimeEpisodeID])
                            {
                                UserStats us = new UserStats();
                                us.JMMUserId = aeu.JMMUserID.ToString();
                                us.PlayedCount = aeu.PlayedCount;
                                us.StoppedCount = aeu.StoppedCount;
                                us.WatchedCount = aeu.WatchedCount;
                                us.WatchedDate = aeu.WatchedDate;
                                e.UsersStats.Add(us);
                            }
                        }

                        if (aeps.ContainsKey(ep.AniDB_EpisodeID))
                        {
                            JMMServer.Entities.AniDB_Episode aep = aeps[ep.AniDB_EpisodeID];
                            AniDB_Episode r = new AniDB_Episode();
                            if (aep.AirDate != 0)
                                r.AirDate = ((long) aep.AirDate).ToDateTime();
                            r.EnglishName = aep.EnglishName;
                            r.Id = aep.EpisodeID.ToString();
                            r.EpisodeOrder = 1;
                            r.IsSplit = false;
                            r.LengthSeconds = aep.LengthSeconds;
                            r.Number = aep.EpisodeNumber;
                            r.Percentage = 100F;
                            r.Rating = 0;
                            if (!string.IsNullOrEmpty(aep.Rating))
                                r.Rating = (float) int.Parse(aep.Rating)/100F;
                            r.RomajiName = aep.RomajiName;
                            r.Type = (AniDB_Episode_Type) aep.EpisodeType;
                            r.DateTimeCreated = r.DateTimeUpdated = aep.DateTimeUpdated;
                            e.AniDbEpisodes.Add(r.Id, new List<AniDB_Episode> {r});
                            r.MyVotes = new List<AniDB_Vote>();
                            if (aves.ContainsKey(ep.AniDB_EpisodeID))
                            {
                                foreach (JMMServer.Entities.AniDB_Vote v in aves[ep.AniDB_EpisodeID])
                                    r.MyVotes.Add(new AniDB_Vote
                                    {
                                        JMMUserId = _users[0].Id,
                                        Type = (AniDB_Vote_Type) v.VoteType,
                                        Vote = (float) v.VoteValue/100F
                                    });
                            }
                            r.Files = new List<AniDB_File>();
                            r.VideoLocals = new List<VideoLocal>();
                            if (cfes.ContainsKey(ep.AnimeEpisodeID))
                            {
                                foreach (JMMServer.Entities.CrossRef_File_Episode cfe in cfes[ep.AnimeEpisodeID])
                                {
                                    if (afs.ContainsKey(cfe.Hash))
                                    {
                                        JMMServer.Entities.AniDB_File ff = afs[cfe.Hash];
                                        AniDB_File f = new AniDB_File();
                                        f.GroupName = ff.Anime_GroupName;
                                        f.CrossRefSource = (CrossRefSourceType) cfe.CrossRefSource;
                                        f.GroupNameShort = ff.Anime_GroupNameShort;
                                        f.CRC = ff.CRC;
                                        f.Rating = ff.Episode_Rating/100F;
                                        f.Votes = ff.Episode_Votes;
                                        f.FileId = ff.FileID;
                                        f.FileName = ff.FileName;
                                        f.FileSize = ff.FileSize;
                                        f.FileVersion = ff.FileVersion;
                                        f.AudioCodec = ff.File_AudioCodec;
                                        f.Description = ff.File_Description;
                                        f.FileExtension = ff.File_FileExtension;
                                        f.LengthSeconds = ff.File_LengthSeconds;
                                        f.ReleaseDate = ((long) ff.File_ReleaseDate).ToDateTime();
                                        f.Source = ff.File_Source;
                                        f.FileName = ff.FileName;
                                        f.VideoCodec = ff.File_VideoCodec;
                                        f.VideoResolution = ff.File_VideoResolution;
                                        f.GroupId = ff.GroupID.ToString();
                                        f.Hash = ff.Hash;
                                        f.InternalVersion = ff.InternalVersion;
                                        f.IsCensored = ff.IsCensored != 0;
                                        f.IsDeprecated = ff.IsDeprecated != 0;
                                        f.UserStats=new List<UserStats>();
                                        f.UserStats.Add(new UserStats {  JMMUserId = _users[0].Id,WatchedCount = ff.IsWatched, WatchedDate = ff.WatchedDate});
                                        f.MD5 = ff.MD5;
                                        f.SHA1 = ff.SHA1;
                                        f.DateTimeCreated = f.DateTimeUpdated = ff.DateTimeUpdated;
                                        f.Languages = new List<Language>();
                                        f.Subtitles = new List<Language>();
                                        if (clafs.ContainsKey(f.FileId))
                                        {
                                            foreach (
                                                JMMServer.Entities.CrossRef_Languages_AniDB_File l in clafs[f.FileId])
                                                f.Languages.Add(new Language
                                                {
                                                    Id = l.LanguageID.ToString(),
                                                    Name = _langs[l.LanguageID].Name
                                                });
                                        }
                                        if (csafs.ContainsKey(f.FileId))
                                        {
                                            foreach (
                                                JMMServer.Entities.CrossRef_Subtitles_AniDB_File l in csafs[f.FileId])
                                                f.Subtitles.Add(new Language
                                                {
                                                    Id = l.LanguageID.ToString(),
                                                    Name = _langs[l.LanguageID].Name
                                                });
                                        }
                                        r.Files.Add(f);
                                    }
                                    if (vls.ContainsKey(cfe.Hash) && _videos.ContainsKey(cfe.Hash))
                                    {
                                        JMMServer.Entities.VideoLocal vv = vls[cfe.Hash];
                                        VideoInfo vi = _videos[cfe.Hash];
                                        VideoLocal v = new VideoLocal();
                                        ((MediaInfo) vi).CopyTo(v);
                                        v.FileInfo = new FileInfo
                                        {
                                            ImportFolderId = vv.ImportFolderID.ToString(),
                                            Path = vv.FilePath
                                        };
                                        v.CRC = vv.CRC32;
                                        v.MD5 = vv.MD5;
                                        v.SHA1 = vv.SHA1;
                                        v.CrossRefSource = (CrossRefSourceType) cfe.CrossRefSource;
                                        v.FFdShowPreset = new HashSet<string>();
                                        v.HashSource = (HashSource) vv.HashSource;
                                        v.IsIgnored = vv.IsIgnored != 0;
                                        v.IsVariation = vv.IsVariation != 0;
                                        if (ffdps.ContainsKey(cfe.Hash))
                                        {
                                            foreach (JMMServer.Entities.FileFfdshowPreset pre in ffdps[cfe.Hash])
                                            {
                                                v.FFdShowPreset.Add(pre.Preset);
                                            }
                                        }
                                        v.UsersStats = new List<UserStats>();
                                        if (vlus.ContainsKey(vv.VideoLocalID))
                                        {
                                            foreach (JMMServer.Entities.VideoLocal_User aeu in vlus[vv.VideoLocalID])
                                            {
                                                UserStats us = new UserStats();
                                                us.JMMUserId = aeu.JMMUserID.ToString();
                                                us.PlayedCount = 1;
                                                us.StoppedCount = 0;
                                                us.WatchedCount = 1;
                                                us.WatchedDate = aeu.WatchedDate;
                                                e.UsersStats.Add(us);
                                            }
                                        }
                                    }

                                }
                            }
                        }
                    }
                    if (s.IsMovie)
                    {
                        //Fix multipart episodes
                        Dictionary<int, Dictionary<int, AnimeEpisode>> grouped =
                            new Dictionary<int, Dictionary<int, AnimeEpisode>>();
                        foreach (AnimeEpisode ae in s.Episodes)
                        {
                            string m =
                                ae.AniDbEpisodes.Values.ElementAt(0)[0].RomajiName.ToLower(
                                    System.Globalization.CultureInfo.InvariantCulture);
                            m = remsymbols.Replace(m, string.Empty);
                            m = remmultispace.Replace(m, string.Empty);
                            Match mt = partmatch.Match(m);
                            if (mt.Success)
                            {
                                int first = 0;
                                int second = 0;
                                int.TryParse(mt.Groups["first"].Value, out first);
                                int.TryParse(mt.Groups["second"].Value, out second);
                                if (first > 0 && second > 0)
                                {
                                    Dictionary<int, AnimeEpisode> sub;
                                    if (grouped.ContainsKey(second))
                                        sub = grouped[second];
                                    else
                                    {
                                        sub = new Dictionary<int, AnimeEpisode>();
                                        grouped.Add(second, sub);
                                    }

                                    sub.Add(first, ae);
                                }
                            }
                        }
                        if (grouped.Count > 0)
                        {
                            List<AnimeEpisode> laes = grouped.Values.SelectMany(c => c.Values).ToList();
                            int cnt = 0;
                            foreach (AnimeEpisode ae in s.Episodes)
                            {
                                if (!laes.Contains(ae))
                                {
                                    cnt++;
                                    if (cnt == 2)
                                        throw new Exception("Multipart error");
                                    grouped.Add(1, new Dictionary<int, AnimeEpisode> {{1, ae}});
                                }
                            }
                            s.Episodes = new List<AnimeEpisode>();
                            AnimeEpisode eee = new AnimeEpisode();
                            eee.UsersStats = new List<UserStats>();
                            s.Episodes.Add(eee);
                            eee.AniDbEpisodes = new Dictionary<string, List<AniDB_Episode>>();
                            bool first = true;
                            foreach (int n in grouped.Keys)
                            {
                                Dictionary<int, AnimeEpisode> ls =
                                    grouped[n].OrderBy(z => z.Key).ToDictionary(z => z.Key, z => z.Value);
                                int totalseconds = ls.Values.Sum(z => z.AniDbEpisodes[0][0].LengthSeconds);
                                foreach (int k in ls.Keys)
                                {
                                    AnimeEpisode ae = ls[k];
                                    if (first)
                                    {
                                        eee.Id = ae.Id;
                                        first = false;
                                    }
                                    if (ae.UsersStats != null && ae.UsersStats.Count > 0)
                                    {
                                        foreach (UserStats us in ae.UsersStats)
                                        {
                                            if (!eee.UsersStats.Any(z => z.JMMUserId == us.JMMUserId))
                                            {
                                                eee.UsersStats.Add(us);
                                            }
                                        }
                                    }
                                    AniDB_Episode ep = ae.AniDbEpisodes[0][0];
                                    IList<AniDB_Episode> cl = new List<AniDB_Episode>();
                                    if (eee.AniDbEpisodes.ContainsKey(n))
                                        cl = eee.AniDbEpisodes[n];
                                    else
                                        eee.AniDbEpisodes.Add(n, cl);
                                    ep.IsSplit = n > 1;
                                    ep.EpisodeOrder = k;
                                    ep.Percentage = (float) ep.LengthSeconds*100F/(float) totalseconds;
                                    cl.Add(ep);
                                }
                            }
                        }
                    }
                }
                if ((a.TvDBs != null) && (a.TvDBs.Count > 0))
                {
                    foreach (AniDB_Anime_TvDB aat in a.TvDBs)
                    {
                        List<JMMServer.Entities.TvDB_Episode> eps =
                            tves[aat.TvDBSeriesId].Where(
                                z =>
                                    z.SeasonID == aat.ProviderSeasonNumber &&
                                    z.EpisodeNumber >= aat.ProviderStartEpisodeNumber).ToList();
                        Dictionary<int, AnimeEpisode> zaeps = new Dictionary<int, AnimeEpisode>();
                        foreach (AnimeEpisode x in s.Episodes)
                        {
                            AniDB_Episode ep = x.AniDbEpisodes[0][0];
                            if (ep.Type == aat.StartEpisodeType && ep.Number >= aat.StartEpisodeNumber)
                                zaeps.Add(ep.Number, x);
                        }

                        foreach (JMMServer.Entities.TvDB_Episode tve in eps)
                        {
                            int rp = tve.EpisodeNumber - aat.ProviderStartEpisodeNumber + aat.StartEpisodeNumber;
                            if (zaeps.ContainsKey(rp))
                            {
                                AnimeEpisode r = zaeps[rp];
                                r.TvDBEpisode = new Episode_TvDBEpisode();
                                r.TvDBEpisode.AbsoluteNumber = tve.AbsoluteNumber;
                                r.TvDBEpisode.AirsAfterSeason = tve.AirsAfterSeason;
                                r.TvDBEpisode.AirsBeforeEpisode = tve.AirsBeforeEpisode;
                                r.TvDBEpisode.AirsBeforeSeason = tve.AirsBeforeSeason;
                                r.TvDBEpisode.EpImgFlag = tve.EpImgFlag;
                                r.TvDBEpisode.EpisodeName = tve.EpisodeName;
                                r.TvDBEpisode.EpisodeNumber = tve.EpisodeNumber;
                                r.TvDBEpisode.Filename = tve.Filename;
                                r.TvDBEpisode.Id = tve.TvDB_EpisodeID;
                                r.TvDBEpisode.Overview = tve.Overview;
                                r.TvDBEpisode.SeasonId = tve.SeasonID;
                                r.TvDBEpisode.SeasonNumber = tve.SeasonNumber;
                                r.TvDBEpisode.SeriesId = tve.SeriesID;
                                ProcessImage(tve.FullImagePath, r.TvDBEpisode, ImageType.Episode, ImageSourceTypes.TvDB,
                                    true);
                            }
                        }
                    }
                }
                if ((a.Trakts != null && (a.Trakts.Count > 0)))
                {
                    foreach (AniDB_Anime_Trakt aat in a.Trakts)
                    {
                        List<JMMServer.Entities.Trakt_Episode> eps =
                            tres[int.Parse(aat.TraktId)].Where(
                                z =>
                                    z.Season == aat.ProviderSeasonNumber &&
                                    z.EpisodeNumber >= aat.ProviderStartEpisodeNumber).ToList();
                        Dictionary<int, AnimeEpisode> zaeps = new Dictionary<int, AnimeEpisode>();
                        foreach (AnimeEpisode x in s.Episodes)
                        {
                            AniDB_Episode ep = x.AniDbEpisodes[0][0];
                            if (ep.Type == aat.StartEpisodeType && ep.Number >= aat.StartEpisodeNumber)
                                zaeps.Add(ep.Number, x);
                        }

                        foreach (JMMServer.Entities.Trakt_Episode tve in eps)
                        {
                            int rp = tve.EpisodeNumber - aat.ProviderStartEpisodeNumber + aat.StartEpisodeNumber;
                            if (zaeps.ContainsKey(rp))
                            {
                                AnimeEpisode r = zaeps[rp];
                                r.TraktEpisode = new Episode_TraktEpisode();
                                r.TraktEpisode.Id = tve.TraktID.Value;
                                r.TraktEpisode.EpisodeImage = tve.EpisodeImage;
                                r.TraktEpisode.Number = tve.EpisodeNumber;
                                r.TraktEpisode.Overview = tve.Overview;
                                r.TraktEpisode.ShowId = tve.Trakt_ShowID;
                                r.TraktEpisode.Season = tve.Season;
                                r.TraktEpisode.Title = tve.Title;
                                r.TraktEpisode.Url = tve.URL;
                                ProcessImage(tve.FullImagePath, r.TraktEpisode, ImageType.Episode, ImageSourceTypes.TraktDB, true);
                            }
                        }

                    }
                }
                s.Save(session,~UpdateType.ParentGroup);
                session.Store(s);
                _series.Add(s.Id, s);
                if (!_grpseries.ContainsKey(s.GroupId))
                    _grpseries.Add(s.GroupId,new List<AnimeSerie>());
                _grpseries[s.GroupId].Add(s);
            }
            session.SaveChanges();
            session.Dispose();
        }

        public static void ConvertVideoInfos()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.VideoInfo> vis = GetAll<JMMServer.Entities.VideoInfo>();

            foreach (JMMServer.Entities.VideoInfo vi in vis)
            {
                VideoInfo v =new VideoInfo();
                v.Id = vi.Hash;
                v.AudioBitrate = vi.AudioBitrate;
                v.AudioCodec = vi.AudioCodec;
                v.DateTimeCreated = v.DateTimeUpdated = vi.DateTimeUpdated;
                v.Duration = vi.Duration;
                v.FileSize = vi.FileSize;
                v.FullInfo = vi.FullInfo;
                v.Hash = vi.Hash;
                v.VideoBitDepth = vi.VideoBitDepth;
                v.VideoBitrate = vi.VideoBitrate;
                v.VideoCodec = vi.VideoCodec;
                v.VideoFrameRate = vi.VideoFrameRate;
                v.VideoResolution = vi.VideoResolution;
                if (!_videos.ContainsKey(v.Hash))
                {
                    _videos.Add(v.Hash, v);
                    session.Store(v);
                }
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertVersion()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.Versions> tfs = GetAll<JMMServer.Entities.Versions>();

            foreach (JMMServer.Entities.Versions su in tfs)
            {
                Version v = new Version();
                v.Id = su.VersionsID.ToString();
                v.Type = VersionType.Database;
                v.Value = su.VersionValue;
                session.Store(v);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertTraktFriends()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.Trakt_Friend> tfs = GetAll<JMMServer.Entities.Trakt_Friend>();

            foreach (JMMServer.Entities.Trakt_Friend su in tfs)
            {
                Trakt_Friend t=new Trakt_Friend();
                t.About = su.About;
                t.Age = su.Age;
                t.Avatar = su.Avatar;
                t.FullName = su.FullName;
                t.Gender = su.Gender;
                t.Id = su.Trakt_FriendID.ToString();
                t.Joined = su.Joined != 0;
                t.LastAvatarUpdate = su.LastAvatarUpdate;
                t.Location = su.Location;
                t.Url = su.Url;
                t.UserName = su.Username;
                t.FullImagePath = su.FullImagePath;
                session.Store(t);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertScheduleUpdate()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.ScheduledUpdate> sus = GetAll<JMMServer.Entities.ScheduledUpdate>();

            foreach (JMMServer.Entities.ScheduledUpdate su in sus)
            {
                ScheduledUpdate s=new ScheduledUpdate();
                s.Id = su.ScheduledUpdateID.ToString();
                s.LastUpdate = su.LastUpdate;
                s.Type = (ScheduledUpdateType) su.UpdateType;
                s.Details = su.UpdateDetails;
                session.Store(s);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertRenameScripts()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.RenameScript> rss = GetAll<JMMServer.Entities.RenameScript>();

            foreach (JMMServer.Entities.RenameScript rs in rss)
            {
                RenameScript r=new RenameScript();
                r.Id = rs.RenameScriptID.ToString();
                r.IsEnabledOnImport = rs.IsEnabledOnImport != 0;
                r.Name = rs.ScriptName;
                r.Script = rs.Script;
                session.Store(r);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertLogMessages()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.LogMessage> lms = GetAll<JMMServer.Entities.LogMessage>();

            foreach (JMMServer.Entities.LogMessage lm in lms)
            {
                LogMessage l=new LogMessage();
                l.Content = lm.LogContent;
                l.Id = lm.LogMessageID.ToString();
                l.TimeStamp = lm.LogDate;
                l.Type = lm.LogType;
                session.Store(l);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertImportFolder()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.ImportFolder> inf = GetAll<JMMServer.Entities.ImportFolder>();

            foreach (JMMServer.Entities.ImportFolder i in inf)
            {
                ImportFolder f=new ImportFolder();
                f.Id = i.ImportFolderID.ToString();
                f.IsDropDestination = i.IsDropDestination != 0;
                f.IsDropSource = i.IsDropSource != 0;
                f.IsWatched = i.IsWatched != 0;
                f.Location = i.ImportFolderLocation;
                f.Name = i.ImportFolderName;
                f.Type = (ImportFolderType) i.ImportFolderType;
                session.Store(f);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertFileNameHash()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.FileNameHash> df = GetAll<JMMServer.Entities.FileNameHash>();

            foreach (JMMServer.Entities.FileNameHash fn in df)
            {
                FileNameHash f=new FileNameHash();
                f.Id = fn.FileNameHashID.ToString();
                f.DateTimeUpdated = fn.DateTimeUpdated;
                f.FileName = fn.FileName;
                f.FileSize = fn.FileSize;
                f.Hash = fn.Hash;
                session.Store(f);
            }
            session.SaveChanges();
            session.Dispose();
        }

        public static void ConvertDuplicateFiles()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.DuplicateFile> df = GetAll<JMMServer.Entities.DuplicateFile>();

            foreach (JMMServer.Entities.DuplicateFile d in df)
            {
                DuplicateFile f=new DuplicateFile();
                f.DateTimeUpdated = d.DateTimeUpdated;
                f.Id = d.DuplicateFileID.ToString();
                f.Duplicates=new List<FileInfo>();
                f.Duplicates.Add(new FileInfo { ImportFolderId = d.ImportFolderIDFile1.ToString(), Path = d.FilePathFile1 });
                f.Duplicates.Add(new FileInfo { ImportFolderId = d.ImportFolderIDFile2.ToString(), Path = d.FilePathFile2 });
                session.Store(f);
            }
            session.SaveChanges();
            session.Dispose();
        }

        public static void ConvertCommandRequest()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.CommandRequest> crs = GetAll<JMMServer.Entities.CommandRequest>();
            foreach (JMMServer.Entities.CommandRequest r in crs)
            {
                CommandRequest c=new CommandRequest();
                c.CommandType = (CommandRequestType) r.CommandType;
                c.Id = r.CommandID;
                c.DateTimeUpdated = r.DateTimeUpdated;
                c.Metadata = JMMServer.Commands.CommandHelper.GetCommand(r);
                c.Priority = (CommandRequestPriority) r.Priority;
                session.Store(c);
            }
            session.SaveChanges();
            session.Dispose();
        }


        public static void ConvertBookmarkedAnime()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.BookmarkedAnime> bks = GetAll<JMMServer.Entities.BookmarkedAnime>();
            foreach (JMMServer.Entities.BookmarkedAnime bk in bks)
            {
                BookmarkedAnime b=new BookmarkedAnime();
                b.AnimeId = bk.AnimeID.ToString();
                b.Downloading = bk.Downloading != 0;
                b.Id = bk.BookmarkedAnimeID.ToString();
                b.Notes = bk.Notes;
                b.Priority = bk.Priority;
                session.Store(b);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertReleaseGroups()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.AniDB_ReleaseGroup> grps = GetAll<JMMServer.Entities.AniDB_ReleaseGroup>();
            foreach (JMMServer.Entities.AniDB_ReleaseGroup g in grps)
            {
                AniDB_ReleaseGroup gg=new AniDB_ReleaseGroup();
                gg.AnimeCount = g.AnimeCount;
                gg.FileCount = g.FileCount;
                gg.Name = g.GroupName;
                gg.ShortName = g.GroupNameShort;
                gg.IRCChannel = g.IRCChannel;
                gg.IRCServer = g.IRCServer;
                gg.Id = g.GroupID.ToString();
                gg.MyVotes=new List<AniDB_Vote>();
                gg.PicName = g.Picname;
                session.Store(gg);
                _rgroups.Add(gg);
            }
            session.SaveChanges();
            session.Dispose();
        }

        private static IList<T> GetAll<T>()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(T)).List<T>();
            }
        }


        public static void ConvertLanguages()
        {
            IDocumentSession session = Store.GetSession();
            foreach (JMMServer.Entities.Language l in GetAll<JMMServer.Entities.Language>())
            {
                Language lan=new Language();
                lan.Id = l.LanguageID.ToString();
                lan.Name = l.LanguageName;
                session.Store(lan);
                _langs.Add(l.LanguageID, lan);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertAniDBCreators()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.AniDB_Seiyuu> seis=GetAll<JMMServer.Entities.AniDB_Seiyuu>();
            string appPath = Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Images");
            foreach (JMMServer.Entities.AniDB_Seiyuu sei in seis)
            {
                AniDB_Creator c=new AniDB_Creator();
                c.Id = sei.SeiyuuID.ToString();
                c.CreatorType=AniDB_Creator_Type.Person;
                c.RomajiName = sei.SeiyuuName;
                c.PicName = sei.PicName;
                ProcessImage(sei.PosterPath, c, ImageType.Poster, ImageSourceTypes.AniDB, true);
                session.Store(c);
                _creators.Add(sei.SeiyuuID, c);
            }
            session.SaveChanges();
            session.Dispose();
        }


        public static void ConvertAniDBCharacters()
        {
            IDocumentSession session = Store.GetSession();
            Dictionary<int,List<JMMServer.Entities.AniDB_Anime_Character>> acl=GetAll<JMMServer.Entities.AniDB_Anime_Character>().GroupBy(a=>a.CharID).ToDictionary(a=>a.Key,a=>a.ToList());
            Dictionary<int,List<string>> see=GetAll<JMMServer.Entities.AniDB_Character_Seiyuu>().GroupBy(a=>a.CharID).ToDictionary(a=>a.Key,a=>a.Select(ba=>ba.SeiyuuID.ToString()).ToList());
            IList<JMMServer.Entities.AniDB_Character> chars = GetAll<JMMServer.Entities.AniDB_Character>();
            string appPath = Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Images");

            foreach (JMMServer.Entities.AniDB_Character ch in chars)
            {
                AniDB_Character c=new AniDB_Character();
                c.Animes=new List<AnimeWithCreatorInfo>();
                c.EpisodeIds=new HashSet<string>(); //All EpisodeListRaw are empty :(
                List<string> seiids = see.ContainsKey(ch.CharID) ? see[ch.CharID] : new List<string>();
                List<AniDB_Creator> crs = _creators.Values.Where(a => seiids.Contains(a.Id)).ToList();

                if (acl.ContainsKey(ch.CharID))
                {
                    foreach (JMMServer.Entities.AniDB_Anime_Character rc in acl[ch.CharID])
                    {
                        //Partially wrong, Make DateTimeUpdate/creation very low

                        foreach (AniDB_Creator cr in crs)
                        {
                            AnimeWithCreatorInfo cq = new AnimeWithCreatorInfo();
                            cr.CopyTo(cq);
                            cq.AniDBId = rc.AnimeID.ToString();
                            if (rc.CharType.Contains("main"))
                                cq.ApparenceType = AniDB_Apparence_Type.MainCharacter;
                            else if (rc.CharType.Contains("secondary"))
                                cq.ApparenceType = AniDB_Apparence_Type.SecondaryCast;
                            else if (rc.CharType.Contains("appears"))
                                cq.ApparenceType = AniDB_Apparence_Type.Appears;
                            else
                                cq.ApparenceType = AniDB_Apparence_Type.Cameo;
                            cq.CreatorType = AniDB_Creator_Type.Person;
                            c.Animes.Add(cq);
                            cq.LastUpdate = DateTime.MinValue;
                        }
                    }
                }
                c.RomajiName = ch.CharName;
                c.KanjiName = ch.CharKanjiName;
                c.PicName = ch.PicName;
                ProcessImage(ch.PosterPath, c, ImageType.Poster, ImageSourceTypes.AniDB, true);
                c.Description = ch.CharDescription;
                c.Id = ch.CharID.ToString();
                c.LastUpdate=DateTime.MinValue;
                session.Store(c);
                _chars.Add(ch.CharID, c);
            }
            session.SaveChanges();
            session.Dispose();
        }


        public static void ConvertTags()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.AniDB_Tag> tags = GetAll<JMMServer.Entities.AniDB_Tag>();
            IList<JMMServer.Entities.CustomTag> ctags = GetAll<JMMServer.Entities.CustomTag>();
            foreach (JMMServer.Entities.AniDB_Tag t in tags)
            {
                JMMModels.AniDB_Tag tag=new JMMModels.AniDB_Tag();
                tag.Count = t.TagCount;
                tag.Description = t.TagDescription;
                tag.GlobalSpoiler = t.GlobalSpoiler!=0;
                tag.Id = t.TagID.ToString();
                tag.LocalSpoiler = t.LocalSpoiler != 0;
                tag.Name = t.TagName;
                tag.Spoiler = t.Spoiler != 0;
                session.Store(tag);
                _tags.Add(t.TagID, tag);
            }
            foreach (JMMServer.Entities.CustomTag t in ctags)
            {
                JMMModels.CustomTag tag = new JMMModels.CustomTag();
                tag.Description = t.TagDescription;
                tag.Id = t.CustomTagID .ToString();
                tag.Name = t.TagName;
                tag.IsSystemTag = true;
                session.Store(tag);
                _CustomTags.Add(tag.Id, tag);
            }
            session.SaveChanges();
            session.Dispose();
        }

        public static void ConvertUsers()
        {
            IDocumentSession session = Store.GetSession();
            JMMUserRepository userRepo=new JMMUserRepository();
            List<JMMServer.Entities.JMMUser> users = userRepo.GetAll();
            int cnt = 0;
            string firstid=null;
            foreach (JMMServer.Entities.JMMUser user in users)
            {
                JMMUser u=new JMMUser();

                u.Authorizations=new List<object>();
                if (cnt == 0)
                {
                    firstid = user.JMMUserID.ToString();
                    AniDBAuthorization a=new AniDBAuthorization();
                    a.UserName = JMMServer.ServerSettings.AniDB_Username;
                    a.Password = JMMServer.ServerSettings.AniDB_Password;
                    a.Provider=AuthorizationProvider.AniDB;
                    if (!string.IsNullOrEmpty(JMMServer.ServerSettings.AniDB_AVDumpKey))
                        a.AniDB_AVDumpKey=JMMServer.ServerSettings.AniDB_AVDumpKey;
                    u.Authorizations.Add(a);
                    if (!string.IsNullOrEmpty(JMMServer.ServerSettings.MAL_Username))
                    {
                        UserNameAuthorization b=new UserNameAuthorization();
                        b.UserName = JMMServer.ServerSettings.MAL_Username;
                        b.Password = JMMServer.ServerSettings.MAL_Password;
                        b.Provider=AuthorizationProvider.MAL;
                        u.Authorizations.Add(b);
                    }
                    if (!string.IsNullOrEmpty(JMMServer.ServerSettings.Trakt_AuthToken))
                    {
                        TraktAuthorization b=new TraktAuthorization();
                        b.Provider = AuthorizationProvider.Trakt;
                        b.Trakt_AuthToken=JMMServer.ServerSettings.Trakt_AuthToken;
                        b.Trakt_RefreshToken=JMMServer.ServerSettings.Trakt_RefreshToken;
                        b.Trakt_TokenExpirationDate=JMMServer.ServerSettings.Trakt_TokenExpirationDate;
                        u.Authorizations.Add(b);
                    }
                }
                u.CanEditServerSettings=user.CanEditServerSettings.HasValue && user.CanEditServerSettings.Value!=0;
                u.RestrictedCustomTagsIds=new List<string>();
                u.RestrictedTagsIds=new List<string>();
                if (!string.IsNullOrEmpty(user.HideCategories))
                {
                    string[] n = user.HideCategories.Split(',');
                    foreach (string s in n)
                    {
                        string c = s.ToUpper();
                        JMMModels.AniDB_Tag t = _tags.Values.FirstOrDefault(a => a.Name.ToUpper() == c);
                        if (t!=null)
                            u.RestrictedTagsIds.Add(t.Id);
                    }
                }
                u.Id = user.JMMUserID.ToString();
                u.IsAdmin = user.IsAdmin != 0;
                u.IsPasswordRequired = !string.IsNullOrEmpty(user.Password);
                u.ParentId = firstid;
                u.CanVoteAsParent = true;
                u.IsMasterAccount = (cnt == 0);
                if (cnt == 0)
                    firstid = user.JMMUserID.ToString();
                u.Password = user.Password;
                u.UserName = user.Username;

                session.Store(u);
                _users.Add(u);
            }
            session.SaveChanges();
            session.Dispose();
        }
        public static void ConvertGroupFilters()
        {

            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.GroupFilter> gf = GetAll<JMMServer.Entities.GroupFilter>();
            Dictionary<int,List<JMMServer.Entities.GroupFilterCondition>> gfc=GetAll<JMMServer.Entities.GroupFilterCondition>().GroupBy(a=>a.GroupFilterID).ToDictionary(a=>a.Key,a=>a.ToList());
            foreach (JMMServer.Entities.GroupFilter g in gf)
            {
                GroupFilter n=new GroupFilter();
                n.ApplyToSeries = g.ApplyToSeries!=0;
                n.BaseCondition = (GroupFilterBaseCondition) g.BaseCondition;
                n.Id = g.GroupFilterID.ToString();
                n.Locked = g.Locked!=0;
                n.Name = g.GroupFilterName;
                n.SortingCriteria=new List<GroupFilterSortCriteria>();
                string[] spl = g.SortingCriteria.Split('|');
                foreach (string s in spl)
                {
                    string[] nk = s.Split(';');
                    GroupFilterSortCriteria sc=new GroupFilterSortCriteria();
                    sc.Sorting = (GroupFilterSorting)int.Parse(nk[0]);
                    sc.Direction=GroupFilterSortDirection.Asc;
                    if (nk.Length > 1)
                        sc.Direction = (GroupFilterSortDirection) int.Parse(nk[1]);
                    n.SortingCriteria.Add(sc);
                }
                n.Conditions=new List<GroupFilterCondition>();
                if (gfc.ContainsKey(g.GroupFilterID))
                {
                    foreach (JMMServer.Entities.GroupFilterCondition cond in gfc[g.GroupFilterID])
                    {
                        GroupFilterCondition c=new GroupFilterCondition();
                        c.Type = (GroupFilterConditionType) cond.ConditionType;
                        c.Operator = (GroupFilterOperator) cond.ConditionOperator;
                        c.Parameter = cond.ConditionParameter;
                        n.Conditions.Add(c);
                    }
                }
                session.Store(n);
                _groupfilter.Add(n);
            }
            session.SaveChanges();
            session.Dispose();

        }
        public static void ConvertGroups()
        {
            IDocumentSession session = Store.GetSession();
            IList<JMMServer.Entities.AnimeGroup> grps = GetAll<JMMServer.Entities.AnimeGroup>();
            Dictionary<int, List<JMMServer.Entities.AnimeSeries>> sers= GetAll<JMMServer.Entities.AnimeSeries>().GroupBy(a=>a.AnimeGroupID).ToDictionary(a=>a.Key,a=>a.ToList());
            Dictionary<int,List<JMMServer.Entities.AnimeGroup_User>> grpus = GetAll<JMMServer.Entities.AnimeGroup_User>().GroupBy(a=>a.AnimeGroupID).ToDictionary(a=>a.Key,a=>a.ToList());
            Dictionary<int, List<JMMServer.Entities.CrossRef_CustomTag>> crc = GetAll<JMMServer.Entities.CrossRef_CustomTag>().Where(a=>a.CrossRefType==2).GroupBy(a=>a.CrossRefID).ToDictionary(a=>a.Key,a=>a.ToList());

            foreach (JMMServer.Entities.AnimeGroup g in grps)
            {
                AnimeGroup grp = new AnimeGroup();
                grp.AnimeSerieIDs = sers.ContainsKey(g.AnimeGroupID) ? sers[g.AnimeGroupID].Select(a => a.AnimeSeriesID.ToString()).ToHashSet() : new HashSet<string>();
                grp.SubGroupsIDs=grps.Where(a=>a.AnimeGroupParentID==g.AnimeGroupID).Select(a=>a.AnimeGroupID.ToString()).ToHashSet();
                grp.CustomTags = crc.ContainsKey(g.AnimeGroupID) ? crc[g.AnimeGroupID].Select(a=>new Anime_Custom_Tag {TagId=a.CustomTagID.ToString(),Name=_CustomTags[a.CustomTagID.ToString()].Name}).ToList() : new List<Anime_Custom_Tag>();
                grp.DefaultAnimeSeriesId = g.DefaultAnimeSeriesID.ToString();
                grp.Description = g.Description;
                grp.EpisodeAddedDate = g.EpisodeAddedDate;
                grp.GroupName = g.GroupName;
                grp.Id = g.AnimeGroupID.ToString();
                grp.IsManuallyNamed = g.IsManuallyNamed!=0;
                grp.OverrideDescription = g.OverrideDescription!=0;
                grp.ParentId = g.AnimeGroupParentID?.ToString();
                grp.SortName = g.SortName;
                grp.DateTimeCreated = g.DateTimeCreated;
                grp.DateTimeUpdated = g.DateTimeUpdated;
                grp.UsersStats=new List<GroupUserStats>();
                if (grpus.ContainsKey(g.AnimeGroupID))
                {
                    foreach (JMMServer.Entities.AnimeGroup_User u in grpus[g.AnimeGroupID])
                    {
                        GroupUserStats ex = new GroupUserStats();
                        ex.IsFave = u.IsFave!=0;
                        ex.UnwatchedEpisodeCount = u.UnwatchedEpisodeCount;
                        ex.WatchedEpisodeCount = u.WatchedEpisodeCount;                        
                        ex.JMMUserId = u.JMMUserID.ToString();
                        ex.PlayedCount = u.PlayedCount;
                        ex.StoppedCount = u.StoppedCount;
                        ex.WatchedCount = u.WatchedCount;
                        ex.WatchedDate = u.WatchedDate;
                        grp.UsersStats.Add(ex);
                    }
                }
                grp.Save(session);
            }
            session.SaveChanges();
            session.Dispose();
        }


       

    }
}
