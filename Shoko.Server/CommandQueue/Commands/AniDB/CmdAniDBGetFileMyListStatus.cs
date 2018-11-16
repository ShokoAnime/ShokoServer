using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBGetFileMyListStatus : BaseCommand, ICommand
    {
        public int AniFileID { get; set; }
        public string FileName { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.AniDB;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 6;
        public string Id => $"GetFileMyListStatus_{AniFileID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.AniDB_MyListGetFile,
            ExtraParams = new[] { FileName, AniFileID.ToString() }
        };

        public string WorkType => WorkTypes.AniDB;

        public CmdAniDBGetFileMyListStatus(string str) : base(str)
        {
        }

        public CmdAniDBGetFileMyListStatus(int aniFileID, string fileName)
        {
            AniFileID = aniFileID;
            FileName = fileName;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Processing CommandRequest_GetFileMyListStatus: {FileName} ({AniFileID})");
            try
            {
                ReportInit(progress);
                ShokoService.AnidbProcessor.GetMyListFileStatus(AniFileID);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDb.GetFileMyListStatus: {FileName} ({AniFileID}) - {ex}", ex);
            }
        }
    }
}