using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.RepositoriesV2.Repos;

namespace Shoko.Server.RepositoriesV2
{
    public static class Repo
    {
        public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; private set; }
        public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; private set; }
        public static AniDB_AnimeRepository AniDB_Anime { get; private set; }
        public static AniDB_EpisodeRepository AniDB_Episode { get; private set; }
        public static AniDB_FileRepository AniDB_File { get; private set; }
        public static AniDB_TagRepository AniDB_Tag { get; private set; }
        public static AniDB_VoteRepository AniDB_Vote { get; private set; }
        public static AnimeEpisodeRepository AnimeEpisode { get; private set; }
        public static AnimeEpisode_UserRepository AnimeEpisode_User { get; private set; }
        public static AnimeSeriesRepository AnimeSeries { get; private set; }
        public static AnimeSeries_UserRepository AnimeSeries_User { get; private set; }
        public static AnimeGroupRepository AnimeGroup { get; private set; }
        public static AnimeGroup_UserRepository AnimeGroup_User { get; private set; }



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

        public static void Init(ShokoContext db, HashSet<string> cachedRepos, IProgress<RegenerateProgress> progress, int batchSize=20)
        {
            _repos=new List<IRepository>();
            if (cachedRepos != null)
                CachedRepos = cachedRepos;
            _db = db;

            AniDB_Anime_Tag = Register<AniDB_Anime_TagRepository,AniDB_Anime_Tag>(db.AniDB_Anime_Tags);
            AniDB_Anime_Title = Register<AniDB_Anime_TitleRepository, AniDB_Anime_Title>(db.AniDB_Anime_Titles);
            AniDB_Anime = Register<AniDB_AnimeRepository, SVR_AniDB_Anime>(db.AniDB_Animes);
            AniDB_Episode = Register<AniDB_EpisodeRepository, AniDB_Episode>(db.AniDB_Episodes);
            AniDB_File = Register<AniDB_FileRepository, SVR_AniDB_File>(db.AniDB_Files);
            AniDB_Tag = Register<AniDB_TagRepository, AniDB_Tag>(db.AniDB_Tags);
            AniDB_Vote = Register<AniDB_VoteRepository, AniDB_Vote>(db.AniDB_Votes);
            AnimeEpisode_User = Register<AnimeEpisode_UserRepository, SVR_AnimeEpisode_User>(db.AnimeEpisode_Users);
            AnimeEpisode = Register<AnimeEpisodeRepository, SVR_AnimeEpisode>(db.AnimeEpisodes);
            AnimeSeries_User = Register<AnimeSeries_UserRepository, SVR_AnimeSeries_User>(db.AnimeSeries_Users);
            AnimeSeries = Register<AnimeSeriesRepository, SVR_AnimeSeries>(db.AnimeSeries);
            AnimeGroup_User = Register<AnimeGroup_UserRepository, SVR_AnimeGroup_User>(db.AnimeGroup_Users);
            AnimeGroup = Register<AnimeGroupRepository, SVR_AnimeGroup>(db.AnimeGroups);

            _repos.ForEach(a=>a.Init(progress,batchSize));
        }

        public static void SetCache(HashSet<string> cachedRepos)
        {
            if (cachedRepos != null)
                CachedRepos = cachedRepos;
            _repos.ForEach(r=>r.SwitchCache(CachedRepos.Contains(r.Name)));
        }
    }
}
