using System;
using System.IO;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

// ReSharper disable HeuristicUnreachableCode

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheSendAnimeXML : BaseCommand<CmdWebCacheSendAnimeXML>, ICommand
    {
        public int AnimeID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendAnimeXML_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnimeAzure,
            extraParams = new[] {AnimeID.ToString()}
        };


        public CmdWebCacheSendAnimeXML(string str) : base(str)
        {
        }

        public CmdWebCacheSendAnimeXML(int animeID)
        {
            AnimeID = animeID;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                bool process = false;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!process) return new CommandResult();
                InitProgress(progress);
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                UpdateAndReportProgress(progress,33);
                if (anime == null) return ReportFinishAndGetResult(progress);

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
                UpdateAndReportProgress(progress, 66);

                Azure_AnimeXML xml = new Azure_AnimeXML
                {
                    AnimeID = AnimeID,
                    AnimeName = anime.MainTitle,
                    DateDownloaded = 0,
                    Username = ServerSettings.Instance.AniDb.Username,
                    XMLContent = rawXML
                };
                AzureWebAPI.Send_AnimeXML(xml);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheSendAnimeXML: {AnimeID} - {ex}", ex);
            }
        }      
    }
}