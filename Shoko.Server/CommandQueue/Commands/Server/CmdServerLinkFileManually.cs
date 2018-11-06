using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.CommandQueue.Commands.AniDB;
using Shoko.Server.CommandQueue.Commands.WebCache;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Server
{

    public class CmdServerLinkFileManually : BaseCommand<CmdServerLinkFileManually>, ICommand
    {
        private new static Logger logger = LogManager.GetCurrentClassLogger();

        public int VideoLocalID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }

        private SVR_AnimeEpisode episode;
        private SVR_VideoLocal vlocal;

        public string ParallelTag { get; set; } = WorkTypes.Server.ToString();
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 3;

        public string Id => $"LinkFileManually_{VideoLocalID}_{EpisodeID}";

        public WorkTypes WorkType => WorkTypes.Server;
        public QueueStateStruct PrettyDescription =>
            new QueueStateStruct
            {
                queueState = QueueStateEnum.LinkFileManually,
                extraParams = new[] { vlocal.Info, episode.Title }
            };


        public CmdServerLinkFileManually(string str) : base(str)
        {
            vlocal = Repo.Instance.VideoLocal.GetByID(VideoLocalID);
            if (null == vlocal)
                throw new Exception($"Videolocal object {VideoLocalID} not found");
            episode = Repo.Instance.AnimeEpisode.GetByID(EpisodeID);

        }

        public CmdServerLinkFileManually(int vidLocalID, int episodeID)
        {
            VideoLocalID = vidLocalID;
            EpisodeID = episodeID;
            vlocal = Repo.Instance.VideoLocal.GetByID(VideoLocalID);
            if (null == vlocal)
                throw new Exception($"Videolocal object {VideoLocalID} not found");
            episode = Repo.Instance.AnimeEpisode.GetByID(EpisodeID);
        }
        public CmdServerLinkFileManually(SVR_VideoLocal vl, SVR_AnimeEpisode ep)
        {
            VideoLocalID = vl.VideoLocalID;
            vlocal = vl;
            episode = ep;
            if (ep!=null)
                EpisodeID = ep.AnimeEpisodeID;
        }
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                InitProgress(progress);
                CrossRef_File_Episode xref = new CrossRef_File_Episode();
                try
                {
                    xref.PopulateManually_RA(vlocal, episode);
                    if (Percentage > 0 && Percentage <= 100)
                    {
                        xref.Percentage = Percentage;
                    }
                }
                catch (Exception ex)
                {
                    return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error populating XREF: {vlocal.ToStringDetailed()}", ex);
                }
                UpdateAndReportProgress(progress,20);
                List<ICommand> cmds=new List<ICommand>();
                Repo.Instance.CrossRef_File_Episode.BeginAdd(xref).Commit();
                CommandQueue.Queue.Instance.Add(new CmdWebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID));
                UpdateAndReportProgress(progress, 40);

                if (ServerSettings.Instance.FileQualityFilterEnabled)
                {
                    List<SVR_VideoLocal> videoLocals = episode.GetVideoLocals();
                    if (videoLocals != null)
                    {
                        videoLocals.Sort(FileQualityFilter.CompareTo);
                        List<SVR_VideoLocal> keep = videoLocals.Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                            .ToList();
                        foreach (SVR_VideoLocal vl2 in keep) videoLocals.Remove(vl2);
                        if (videoLocals.Contains(vlocal)) videoLocals.Remove(vlocal);
                        videoLocals = videoLocals.Where(FileQualityFilter.CheckFileKeep).ToList();

                        foreach (SVR_VideoLocal toDelete in videoLocals)
                        {
                            toDelete.Places.ForEach(a => a.RemoveAndDeleteFile());
                        }
                    }
                }
                UpdateAndReportProgress(progress, 60);

                vlocal.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });

                SVR_AnimeSeries ser;
                using (var upd = Repo.Instance.AnimeSeries.BeginAddOrUpdate(() => episode.GetAnimeSeries()))
                {
                    upd.Entity.EpisodeAddedDate = DateTime.Now;
                    ser = upd.Commit((false, true, false, false));
                }

                //Update will re-save
                ser.QueueUpdateStats();
                UpdateAndReportProgress(progress, 80);

                Repo.Instance.AnimeGroup.BatchAction(ser.AllGroupsAbove, ser.AllGroupsAbove.Count, (grp, _) => grp.EpisodeAddedDate = DateTime.Now);

                if (ServerSettings.Instance.AniDb.MyList_AddFiles)
                {
                    cmds.Add(new CmdAniDBAddFileToMyList(vlocal.Hash));
                }
                if (cmds.Count>0)
                    Queue.Instance.AddRange(cmds);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception e)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing ServerLinkFileManually: {VideoLocalID} - {EpisodeID} - {e}", e);
            }            
        }
    }
}
