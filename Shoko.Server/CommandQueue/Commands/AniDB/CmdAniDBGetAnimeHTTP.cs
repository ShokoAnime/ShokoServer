using System;
using System.Collections.Generic;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.CommandQueue.Preconditions;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBGetAnimeHTTP : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public bool DownloadRelations { get; set; }
        public bool ForceRefresh { get; set; }
        public int RelationDepth { get; set; }



        public QueueStateStruct PrettyDescription => 
            new QueueStateStruct
            {
                QueueState = QueueStateEnum.AnimeInfo,
                ExtraParams = new[] {AnimeID.ToString(), DownloadRelations.ToString(), ForceRefresh.ToString(), RelationDepth.ToString()}
            };
        public string WorkType => WorkTypes.AniDB;
        public string ParallelTag { get; set; } = WorkTypes.AniDB;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 2;
        public string Id => $"GetAnimeHTTP_{AnimeID}";
        public override List<Type> GenericPreconditions => new List<Type> { typeof(AniDBHttpBan) };
        public CmdAniDBGetAnimeHTTP(int animeid, bool forced, bool downloadRelations, int relationDepth = 0)
        {

            AnimeID = animeid;
            ForceRefresh = forced;
            DownloadRelations = downloadRelations;
            RelationDepth = relationDepth;
        }

        // ReSharper disable once UnusedParameter.Local
        public CmdAniDBGetAnimeHTTP(string _) 
        {
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_GetAnimeHTTP: {0}", AnimeID);

            try
            {
                ReportInit(progress);
                ShokoService.AnidbProcessor.GetAnimeInfoHTTP(AnimeID, ForceRefresh, DownloadRelations, RelationDepth);

                // NOTE - related anime are downloaded when the relations are created

                // download group status info for this anime
                // the group status will also help us determine missing episodes for a series

                // download reviews
                /* Deprecated;
                if (ServerSettings.Instance.AniDb.DownloadReviews)
                {
                    Queues.Queue.Instance.Add(new GetR);
                    CommandQueue.Queue.Instance.Add(new CmdGetReviews(AnimeID, ForceRefresh));
                }*/

                // Request an image download
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDB.GetAnimeHTTP: {AnimeID} - {ex}", ex);
            }
        }
    }
}