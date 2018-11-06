using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBGetFileMyListStatus : BaseCommand<CmdAniDBGetFileMyListStatus>, ICommand
    {
        public int AniFileID { get; set; }
        public string FileName { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 6;
        public string Id => $"GetFileMyListStatus_{AniFileID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AniDB_MyListGetFile,
            extraParams = new[] { FileName, AniFileID.ToString() }
        };

        public WorkTypes WorkType => WorkTypes.AniDB;

        public CmdAniDBGetFileMyListStatus(string str) : base(str)
        {
        }

        public CmdAniDBGetFileMyListStatus(int aniFileID, string fileName)
        {
            AniFileID = aniFileID;
            FileName = fileName;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info($"Processing CommandRequest_GetFileMyListStatus: {FileName} ({AniFileID})");
            try
            {
                InitProgress(progress);
                ShokoService.AnidbProcessor.GetMyListFileStatus(AniFileID);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing Command AniDb.GetFileMyListStatus: {FileName} ({AniFileID}) - {ex}", ex);
            }
        }
    }
}