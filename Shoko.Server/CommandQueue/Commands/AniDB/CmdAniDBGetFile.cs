using System;
using AniDBAPI;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBGetFile : BaseCommand<CmdAniDBGetFile>, ICommand
    {
        private SVR_VideoLocal vlocal;


        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }

        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 3;
        public string Id => $"GetFile_{VideoLocalID}";
        public WorkTypes WorkType => WorkTypes.AniDB;
        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();

        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.GetFileInfo, extraParams = new[] { VideoLocalID.ToString(),vlocal != null ? vlocal.Info : VideoLocalID.ToString(), ForceAniDB.ToString() } };

        public CmdAniDBGetFile(string str) : base(str)
        {
            vlocal = Repo.Instance.VideoLocal.GetByID(VideoLocalID);
        }

        public CmdAniDBGetFile(SVR_VideoLocal vl, bool forceAniDB)
        {
            vlocal = vl;
            VideoLocalID = vl.VideoLocalID;
            ForceAniDB = forceAniDB;
        }
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Get AniDB file info: {0}", VideoLocalID);


            try
            {
                InitProgress(progress);
                if (vlocal == null)
                    vlocal = Repo.Instance.VideoLocal.GetByID(VideoLocalID);
                if (vlocal == null) return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"VideoLocal with id {VideoLocalID} not found");
                lock (vlocal)
                {
                    SVR_AniDB_File aniFile = Repo.Instance.AniDB_File.GetByHashAndFileSize(vlocal.Hash, vlocal.FileSize);
                    UpdateAndReportProgress(progress,20);
                    Raw_AniDB_File fileInfo = null;
                    if (aniFile == null || ForceAniDB)
                        fileInfo = ShokoService.AnidbProcessor.GetFileInfo(vlocal);
                    UpdateAndReportProgress(progress,40);

                    if (fileInfo != null)
                    {
                        SVR_AniDB_File file = aniFile;
                        using (var upd = Repo.Instance.AniDB_File.BeginAddOrUpdate(() => file))
                        {
                            upd.Entity.Populate_RA(fileInfo);

                            //overwrite with local file name
                            string localFileName = vlocal.Info;
                            upd.Entity.FileName = localFileName;

                            aniFile = upd.Commit();
                        }

                        UpdateAndReportProgress(progress,55);
                        aniFile.CreateLanguages();
                        UpdateAndReportProgress(progress,70);
                        aniFile.CreateCrossEpisodes(vlocal.Info);
                        UpdateAndReportProgress(progress,85);

                        //TODO: Look at why this might be worth it?
                        //SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(aniFile.AnimeID);
                        //if (anime != null) Repo.Instance.AniDB_Anime.Save(anime); 
                        SVR_AnimeSeries series = Repo.Instance.AnimeSeries.GetByAnimeID(aniFile.AnimeID);
                        series.UpdateStats(true, true, true);
                    }

                    return ReportFinishAndGetResult(progress);
                }
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing Command AniDb.GetFile: {VideoLocalID} - {ex}", ex);
            }
        }
    }
}