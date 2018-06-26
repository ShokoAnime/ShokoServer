using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Repos;

namespace Shoko.Server.Repositories
{
    public static class Repo
    {
        // DECLARE THESE IN ORDER OF DEPENDENCY
        public static JMMUserRepository JMMUser { get; private set; }
        public static AuthTokensRepository AuthTokens { get; private set; }
        public static CloudAccountRepository CloudAccount { get; private set; }
        public static ImportFolderRepository ImportFolder { get; private set; }
        public static AniDB_AnimeRepository AniDB_Anime { get; private set; }
        public static AniDB_EpisodeRepository AniDB_Episode { get; private set; }
        public static AniDB_FileRepository AniDB_File { get; private set; }
        public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; private set; }
        public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; private set; }
        public static AniDB_TagRepository AniDB_Tag { get; private set; }
        public static CustomTagRepository CustomTag { get; private set; }
        public static CrossRef_CustomTagRepository CrossRef_CustomTag { get; private set; }
        public static CrossRef_File_EpisodeRepository CrossRef_File_Episode { get; private set; }
        public static CommandRequestRepository CommandRequest { get; private set; }
        public static VideoLocal_PlaceRepository VideoLocal_Place { get; private set; }
        public static VideoLocalRepository VideoLocal { get; private set; }
        public static VideoLocal_UserRepository VideoLocal_User { get; private set; }
        public static GroupFilterRepository GroupFilter { get; private set; }
        public static AnimeEpisodeRepository AnimeEpisode { get; private set; }
        public static AnimeEpisode_UserRepository AnimeEpisode_User { get; private set; }
        public static AnimeSeriesRepository AnimeSeries { get; private set; }
        public static AnimeSeries_UserRepository AnimeSeries_User { get; private set; }
        public static AnimeGroupRepository AnimeGroup { get; private set; }
        public static AnimeGroup_UserRepository AnimeGroup_User { get; private set; }
        public static AniDB_VoteRepository AniDB_Vote { get; private set; }
        public static TvDB_EpisodeRepository TvDB_Episode { get; private set; }
        public static TvDB_SeriesRepository TvDB_Series { get; private set; }
        public static CrossRef_AniDB_TvDBV2Repository CrossRef_AniDB_TvDBV2 { get; private set; }
        public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode { get; private set; }
        public static TvDB_ImagePosterRepository TvDB_ImagePoster { get; private set; }
        public static TvDB_ImageFanartRepository TvDB_ImageFanart { get; private set; }
        public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner { get; private set; }


        public static Trakt_ShowRepository Trakt_Show { get; private set; }
        public static Trakt_SeasonRepository Trakt_Season { get; private set; }
        public static Trakt_FriendRepository Trakt_Friend { get; private set; }
        public static Trakt_EpisodeRepository Trakt_Episode { get; private set; }
        public static ScheduledUpdateRepository ScheduledUpdate { get; private set; }
        public static RenameScriptRepository RenameScript { get; private set; }
        public static PlaylistRepository Playlist { get; private set; }
        public static MovieDB_PosterRepository MovieDB_Poster { get; private set; }
        public static MovieDB_FanartRepository MovieDB_Fanart { get; private set; }
        public static MovieDB_MovieRepository MovieDb_Movie { get; private set; }
        public static LanguageRepository Language { get; private set; }
        public static IgnoreAnimeRepository IgnoreAnime { get; private set; }
        public static FileNameHashRepository FileNameHash { get; private set; }
        public static FileFfdshowPresetRepository FileFfdshowPreset { get; private set; }
        public static DuplicateFileRepository DuplicateFile { get; private set; }

        public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File { get; private set; }

        public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File { get; private set; }

        public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2 { get; private set; }

        public static CrossRef_AniDB_OtherRepository CrossRef_AniDB_Other { get; private set; }

        public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL { get; private set; }
        public static BookmarkedAnimeRepository BookmarkedAnime { get; private set; }
        public static AniDB_SeiyuuRepository AniDB_Seiyuu { get; private set; }
        public static AniDB_ReviewRepository AniDB_Review { get; private set; }
        public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; private set; }

        public static AniDB_RecommendationRepository AniDB_Recommendation { get; private set; }
        public static AniDB_MylistStatsRepository AniDB_MylistStats { get; private set; }            
        public static AniDB_GroupStatusRepository AniDB_GroupStatus { get; private set; }
        public static AniDB_CharacterRepository AniDB_Character { get; private set; }

        public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu { get; private set; }

        public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; private set; }

        public static AniDB_Anime_ReviewRepository AniDB_Anime_Review { get; private set; }

        public static AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; private set; }

        public static AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage { get; private set; }

        public static AniDB_Anime_CharacterRepository AniDB_Anime_Character { get; private set; }

        public static ScanRepository Scan { get; private set; }
        public static ScanFileRepository ScanFile { get; private set; }
        public static AniDB_Episode_TitleRepository AniDB_Episode_Title { get; internal set; }



        /************** Might need to be DEPRECATED **************/
        public static CrossRef_AniDB_Trakt_EpisodeRepository CrossRef_AniDB_Trakt_Episode { get; private set; }
        public static CrossRef_AniDB_TvDB_Episode_OverrideRepository CrossRef_AniDB_TvDB_Episode_Override { get; private set; }

        //AdHoc Repo
        public static AdhocRepository Adhoc { get; private set; }
        

        private static List<IRepository> _repos;
        private static ShokoContext _db; 

        private static TU Register<TU, T>(DbSet<T> table) where T : class where TU : IRepository<T>, new()
        {
            TU repo = new TU();
            repo.SetContext(_db,table);
            repo.SwitchCache(CachedRepos.Contains(table.GetName()));
            _repos.Add(repo);
            return repo;
        }

        public static HashSet<string> CachedRepos = new HashSet<string>(); //TODO Set Default

        public static void Init(ShokoContext db, HashSet<string> cachedRepos, IProgress<InitProgress> progress, int batchSize=20)
        {
            _repos=new List<IRepository>();
            if (cachedRepos != null)
                CachedRepos = cachedRepos;
            _db = db;



            JMMUser = Register<JMMUserRepository, SVR_JMMUser>(db.JMMUsers);
            AuthTokens = Register<AuthTokensRepository, AuthTokens>(db.AuthTokens);
            CloudAccount = Register<CloudAccountRepository, SVR_CloudAccount>(db.CloudAccounts);
            ImportFolder = Register<ImportFolderRepository, SVR_ImportFolder>(db.ImportFolders);
            AniDB_Anime = Register<AniDB_AnimeRepository, SVR_AniDB_Anime>(db.AniDB_Animes);
            AniDB_Episode = Register<AniDB_EpisodeRepository, AniDB_Episode>(db.AniDB_Episodes);
            AniDB_File = Register<AniDB_FileRepository, SVR_AniDB_File>(db.AniDB_Files);
            AniDB_Anime_Title = Register<AniDB_Anime_TitleRepository, AniDB_Anime_Title>(db.AniDB_Anime_Titles);
            AniDB_Anime_Tag = Register<AniDB_Anime_TagRepository, AniDB_Anime_Tag>(db.AniDB_Anime_Tags);
            AniDB_Tag = Register<AniDB_TagRepository, AniDB_Tag>(db.AniDB_Tags);
            AniDB_Episode_Title = Register<AniDB_Episode_TitleRepository, AniDB_Episode_Title>(db.AniDB_Episode_Title);
            CustomTag = Register<CustomTagRepository, CustomTag>(db.CustomTags);
            CrossRef_CustomTag = Register<CrossRef_CustomTagRepository, CrossRef_CustomTag>(db.CrossRef_CustomTags);
            CrossRef_File_Episode = Register<CrossRef_File_EpisodeRepository, CrossRef_File_Episode>(db.CrossRef_File_Episodes);
            CommandRequest = Register<CommandRequestRepository, CommandRequest>(db.CommandRequests);
            VideoLocal_Place = Register<VideoLocal_PlaceRepository, SVR_VideoLocal_Place>(db.VideoLocal_Places);
            VideoLocal = Register<VideoLocalRepository, SVR_VideoLocal>(db.VideoLocals);
            VideoLocal_User = Register<VideoLocal_UserRepository, VideoLocal_User>(db.VideoLocal_Users);
            GroupFilter = Register<GroupFilterRepository, SVR_GroupFilter>(db.GroupFilters);
            AnimeEpisode = Register<AnimeEpisodeRepository, SVR_AnimeEpisode>(db.AnimeEpisodes);
            AnimeEpisode_User = Register<AnimeEpisode_UserRepository, SVR_AnimeEpisode_User>(db.AnimeEpisode_Users);
            AnimeSeries = Register<AnimeSeriesRepository, SVR_AnimeSeries>(db.AnimeSeries);
            AnimeSeries_User = Register<AnimeSeries_UserRepository, SVR_AnimeSeries_User>(db.AnimeSeries_Users);
            AnimeGroup = Register<AnimeGroupRepository, SVR_AnimeGroup>(db.AnimeGroups);
            AnimeGroup_User = Register<AnimeGroup_UserRepository, SVR_AnimeGroup_User>(db.AnimeGroup_Users);
            AniDB_Vote = Register<AniDB_VoteRepository, AniDB_Vote>(db.AniDB_Votes);
            TvDB_Episode = Register<TvDB_EpisodeRepository, TvDB_Episode>(db.TvDB_Episodes);
            TvDB_Series = Register<TvDB_SeriesRepository, TvDB_Series>(db.TvDB_Series);
            CrossRef_AniDB_TvDBV2 = Register<CrossRef_AniDB_TvDBV2Repository, CrossRef_AniDB_TvDBV2>(db.CrossRef_AniDB_TvDBV2);
            CrossRef_AniDB_TvDB_Episode = Register<CrossRef_AniDB_TvDB_EpisodeRepository, CrossRef_AniDB_TvDB_Episode>(db.CrossRef_AniDB_TvDB_Episodes);
            TvDB_ImagePoster = Register<TvDB_ImagePosterRepository, TvDB_ImagePoster>(db.TvDB_ImagePosters);
            TvDB_ImageFanart = Register<TvDB_ImageFanartRepository, TvDB_ImageFanart>(db.TvDB_ImageFanarts);
            TvDB_ImageWideBanner = Register<TvDB_ImageWideBannerRepository, TvDB_ImageWideBanner>(db.TvDB_ImageWideBanners);


            Trakt_Show = Register<Trakt_ShowRepository, Trakt_Show>(db.Trakt_Shows);
            Trakt_Season = Register<Trakt_SeasonRepository, Trakt_Season>(db.Trakt_Seasons);
            Trakt_Friend = Register<Trakt_FriendRepository, Trakt_Friend>(db.Trakt_Friends);
            Trakt_Episode = Register<Trakt_EpisodeRepository, Trakt_Episode>(db.Trakt_Episodes);
            ScheduledUpdate = Register<ScheduledUpdateRepository, ScheduledUpdate>(db.ScheduledUpdates);
            RenameScript = Register<RenameScriptRepository, RenameScript>(db.RenameScripts);
            Playlist = Register<PlaylistRepository, Playlist>(db.Playlists);
            MovieDB_Poster = Register<MovieDB_PosterRepository, MovieDB_Poster>(db.MovieDB_Posters);
            MovieDB_Fanart = Register<MovieDB_FanartRepository, MovieDB_Fanart>(db.MovieDB_Fanarts);
            MovieDb_Movie = Register<MovieDB_MovieRepository, MovieDB_Movie>(db.MovieDB_Movies);
            Language = Register<LanguageRepository, Language>(db.Languages);
            IgnoreAnime = Register<IgnoreAnimeRepository, IgnoreAnime>(db.IgnoreAnimes);
            FileNameHash = Register<FileNameHashRepository, FileNameHash>(db.FileNameHashes);
            FileFfdshowPreset = Register<FileFfdshowPresetRepository, FileFfdshowPreset>(db.FileFfdshowPresets);
            DuplicateFile = Register<DuplicateFileRepository, DuplicateFile>(db.DuplicateFiles);

            CrossRef_Subtitles_AniDB_File = Register<CrossRef_Subtitles_AniDB_FileRepository, CrossRef_Subtitles_AniDB_File>(db.CrossRef_Subtitles_AniDB_Files);

            CrossRef_Languages_AniDB_File = Register<CrossRef_Languages_AniDB_FileRepository, CrossRef_Languages_AniDB_File>(db.CrossRef_Languages_AniDB_Files);

            CrossRef_AniDB_TraktV2 = Register<CrossRef_AniDB_TraktV2Repository, CrossRef_AniDB_TraktV2>(db.CrossRef_AniDB_TraktV2);

            CrossRef_AniDB_Other = Register<CrossRef_AniDB_OtherRepository, CrossRef_AniDB_Other>(db.CrossRef_AniDB_Other);

            CrossRef_AniDB_MAL = Register<CrossRef_AniDB_MALRepository, CrossRef_AniDB_MAL>(db.CrossRef_AniDB_MALs);
            BookmarkedAnime = Register<BookmarkedAnimeRepository, BookmarkedAnime>(db.BookmarkedAnimes);
            AniDB_Seiyuu = Register<AniDB_SeiyuuRepository, AniDB_Seiyuu>(db.AniDB_Seiyuus);
            AniDB_Review = Register<AniDB_ReviewRepository, AniDB_Review>(db.AniDB_Reviews);
            AniDB_ReleaseGroup = Register<AniDB_ReleaseGroupRepository, AniDB_ReleaseGroup>(db.AniDB_ReleaseGroups);
            AniDB_Recommendation = Register<AniDB_RecommendationRepository, AniDB_Recommendation>(db.AniDB_Recommendations);
            AniDB_MylistStats = Register<AniDB_MylistStatsRepository, AniDB_MylistStats>(db.AniDB_MylistStats);
            AniDB_GroupStatus = Register<AniDB_GroupStatusRepository, AniDB_GroupStatus>(db.AniDB_GroupStatus);
            AniDB_Character = Register<AniDB_CharacterRepository, AniDB_Character>(db.AniDB_Characters);

            AniDB_Character_Seiyuu = Register<AniDB_Character_SeiyuuRepository, AniDB_Character_Seiyuu>(db.AniDB_Character_Seiyuus);

            AniDB_Anime_Similar = Register<AniDB_Anime_SimilarRepository, AniDB_Anime_Similar>(db.AniDB_Anime_Similars);

            AniDB_Anime_Review = Register<AniDB_Anime_ReviewRepository, AniDB_Anime_Review>(db.AniDB_Anime_Reviews);

            AniDB_Anime_Relation = Register<AniDB_Anime_RelationRepository, AniDB_Anime_Relation>(db.AniDB_Anime_Relations);

            AniDB_Anime_DefaultImage = Register<AniDB_Anime_DefaultImageRepository, AniDB_Anime_DefaultImage>(db.AniDB_Anime_DefaultImages);

            AniDB_Anime_Character = Register<AniDB_Anime_CharacterRepository, AniDB_Anime_Character>(db.AniDB_Anime_Characters);

            Scan = Register<ScanRepository, SVR_Scan>(db.Scans);
            ScanFile = Register<ScanFileRepository, ScanFile>(db.ScanFiles);



            /************** Might need to be DEPRECATED **************/
            CrossRef_AniDB_Trakt_Episode = Register<CrossRef_AniDB_Trakt_EpisodeRepository, CrossRef_AniDB_Trakt_Episode>(db.CrossRef_AniDB_Trakt_Episodes);
            Adhoc = new AdhocRepository();

            _repos.ForEach(a => a.PreInit(progress,batchSize));
            _repos.ForEach(a => a.PostInit(progress, batchSize));
        }

        public static void SetCache(HashSet<string> cachedRepos)
        {
            if (cachedRepos != null)
                CachedRepos = cachedRepos;
            _repos.ForEach(r=>r.SwitchCache(CachedRepos.Contains(r.Name)));
        }
    }
}