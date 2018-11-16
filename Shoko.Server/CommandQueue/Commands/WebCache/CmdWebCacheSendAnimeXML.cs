using System;
using System.IO;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.WebCache;
using Shoko.Server.Models;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

// ReSharper disable HeuristicUnreachableCode

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheSendAnimeXML : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.WebCache;
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public string WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendAnimeXML_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SendAnimeAzure,
            ExtraParams = new[] {AnimeID.ToString()}
        };


        public CmdWebCacheSendAnimeXML(string str) : base(str)
        {
        }

        public CmdWebCacheSendAnimeXML(int animeID)
        {
            AnimeID = animeID;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                bool process = false;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!process) return;
                ReportInit(progress);
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                ReportUpdate(progress,33);
                if (anime == null)
                {
                    ReportFinish(progress);
                    return;
                }

                string filePath = ServerSettings.Instance.AnimeXmlDirectory;

                if (!Directory.Exists(filePath))
                    Directory.CreateDirectory(filePath);

                string fileName = $"AnimeDoc_{AnimeID}.xml";
                string fileNameWithPath = Path.Combine(filePath, fileName);

                string rawXML = string.Empty;
                if (File.Exists(fileNameWithPath))
                {
                    StreamReader re = File.OpenText(fileNameWithPath);
                    rawXML = re.ReadToEnd();
                    re.Close();
                }
                ReportUpdate(progress, 66);

                WebCache_AnimeXML xml = new WebCache_AnimeXML
                {
                    AnimeID = AnimeID,
                    AnimeName = anime.MainTitle,
                    DateDownloaded = 0,
                    Username = ServerSettings.Instance.AniDb.Username,
                    XMLContent = rawXML
                };
                WebCacheAPI.Send_AnimeXML(xml);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress,  $"Error processing WebCacheSendAnimeXML: {AnimeID} - {ex}", ex);
            }
        }      
    }
}