using System.Collections.Generic;
using System.Linq;
using JMMDatabase.Extensions;
using JMMDatabase.Repos;
using JMMModels;
using JMMModels.Childs;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Database.Linq.PrivateExtensions;
using Raven.Imports.Newtonsoft.Json;
using AnimeGroup = JMMModels.AnimeGroup;
using AnimeSerie = JMMModels.AnimeSerie;
using GroupFilter = JMMModels.GroupFilter;

namespace JMMDatabase
{

    public static class Store
    {
        public static AnimeGroupRepo AnimeGroupRepo=new AnimeGroupRepo();
        public static AnimeSerieRepo AnimeSerieRepo=new AnimeSerieRepo();
        public static GroupFilterRepo GroupFilterRepo=new GroupFilterRepo();
        public static JMMUserRepo JmmUserRepo=new JMMUserRepo();
        public static ImportFolderRepo ImportFolderRepo=new ImportFolderRepo();
        public static ReleaseGroupRepo ReleaseGroupRepo =new ReleaseGroupRepo();
        public static CommandRequestRepo CommandRequestRepo=new CommandRequestRepo();
        public static ScheduleUpdateRepo ScheduleUpdateRepo=new ScheduleUpdateRepo();
        public static LanguageRepo LanguageRepo=new LanguageRepo();
        public static VideoLocalRepo VideoLocalRepo=new VideoLocalRepo();
        public static VideoInfoRepo VideoInfoRepo=new VideoInfoRepo();
        public static AniDB_FileRepo AniDB_FileRepo=new AniDB_FileRepo();
        public static void Populate(IDocumentSession s)
        {
            AnimeSerieRepo.Populate(s);
            JmmUserRepo.Populate(s);
            AnimeGroupRepo.Populate(s);
            GroupFilterRepo.Populate(s);
            ImportFolderRepo.Populate(s);
            ReleaseGroupRepo.Populate(s);
            CommandRequestRepo.Populate(s);
            ScheduleUpdateRepo.Populate(s);
            LanguageRepo.Populate(s);
            VideoLocalRepo.Populate(s);
            VideoInfoRepo.Populate(s);
            AniDB_FileRepo.Populate(s);
        }


        private static bool init = true;
        private static IDocumentStore Instance { get; } = new EmbeddableDocumentStore
        {
            DataDirectory = "DB",
#if DEBUG
            UseEmbeddedHttpServer = true
#endif
        };

        public static IDocumentSession GetSession()
        {
            if (init)
            {
                Instance.Initialize();
                Instance.Conventions.CustomizeJsonSerializer = serializer => serializer.TypeNameHandling = TypeNameHandling.All;
                init = false;
            }

            return Instance.OpenSession();
        }



    }
}
