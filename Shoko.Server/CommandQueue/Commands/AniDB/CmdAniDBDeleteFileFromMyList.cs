using System;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBDeleteFileFromMyList : BaseCommand, ICommand
    {
        public string Hash { get; set; }
        public long FileSize { get; set; }
        public int MyListID { get; set; }


        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.AniDB_MyListDelete, ExtraParams = new[] {MyListID.ToString(),  Hash, FileSize.ToString()}};
        public WorkTypes WorkType => WorkTypes.AniDB;
        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 10;
        public string Id => $"DeleteFileFromMyList_{Hash}_{MyListID}";

        public CmdAniDBDeleteFileFromMyList(string hash, long filesize)
        {
            Hash = hash;
            FileSize = filesize;
            MyListID = 0;
        }
        public CmdAniDBDeleteFileFromMyList(int mylistId)
        {
            Hash = string.Empty;
            FileSize = 0;
            MyListID = mylistId;
        }
        public CmdAniDBDeleteFileFromMyList(string str) : base(str)
        {

        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            if (MyListID > 0)
                logger.Info("Processing CommandRequest_DeleteFileFromMyList: MyListID: {0}", MyListID);
            else
                logger.Info("Processing CommandRequest_DeleteFileFromMyList: Hash: {0}", Hash);

            try
            {
                InitProgress(progress);
                switch (ServerSettings.Instance.AniDb.MyList_DeleteType)
                {
                    case AniDBFileDeleteType.Delete:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(MyListID);
                            logger.Info("Deleting file from list: MyListID: {0}", MyListID);
                        }
                        else
                        {
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(Hash, FileSize);
                            logger.Info("Deleting file from list: Hash: {0}", Hash);
                        }

                        break;

                    case AniDBFileDeleteType.MarkDeleted:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsDeleted(MyListID);
                            logger.Info("Marking file as deleted from list: MyListID: {0}", MyListID);
                            break;
                        }

                        logger.Warn("File doesn't have a MyListID, can't mark as deleted: {0}", Hash);
                        break;

                    case AniDBFileDeleteType.MarkUnknown:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsUnknown(MyListID);
                            logger.Info("Marking file as unknown: MyListID: {0}", MyListID);
                            break;
                        }

                        logger.Warn("File doesn't have a MyListID, can't mark as unknown: {0}", Hash);
                        break;

                    case AniDBFileDeleteType.DeleteLocalOnly:
                        if (MyListID > 0)
                            logger.Info("Keeping physical file and AniDB MyList entry, deleting from local DB: MyListID: {0}", MyListID);
                        else
                            logger.Info("Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {0}", Hash);
                        break;

                    case AniDBFileDeleteType.MarkExternalStorage:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsRemote(MyListID);
                            logger.Info("Moving file to external storage: MyListID: {0}", MyListID);
                            break;
                        }

                        logger.Warn("File doesn't have a MyListID, can't mark as remote: {0}", Hash);
                        break;
                    case AniDBFileDeleteType.MarkDisk:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsOnDisk(MyListID);
                            logger.Info("Moving file to external storage: MyListID: {0}", MyListID);
                            break;
                        }

                        logger.Warn("File doesn't have a MyListID, can't mark as on disk: {0}", Hash);
                        break;
                }

                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, !string.IsNullOrEmpty(Hash) ? $"Error processing Command AniDB.AddFileToMyList: Hash: {Hash} - {ex}" : $"Error processing Command AniDB.AddFileToMyList: MyListID: {MyListID} - {ex}", ex);
            }
        }
    }
}