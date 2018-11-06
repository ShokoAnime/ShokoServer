using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBUpdateMyListFileStatus : BaseCommand<CmdAniDBUpdateMyListFileStatus>, ICommand
    {
        public string FullFileName { get; set; }
        public string Hash { get; set; }
        public bool Watched { get; set; }
        public bool UpdateSeriesStats { get; set; }
        public int WatchedDateAsSecs { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 6;
        public string Id => $"UpdateMyListFileStatus_{Hash}_{Guid.NewGuid().ToString()}";
        public WorkTypes WorkType => WorkTypes.AniDB;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.UpdateMyListInfo,
            extraParams = new[] {FullFileName, Hash, Watched.ToString(), UpdateSeriesStats.ToString(), WatchedDateAsSecs.ToString()}
        };


        public CmdAniDBUpdateMyListFileStatus(string str) : base(str)
        {
        }

        public CmdAniDBUpdateMyListFileStatus(string hash, bool watched, bool updateSeriesStats, int watchedDateSecs)
        {
            Hash = hash;
            Watched = watched;
            UpdateSeriesStats = updateSeriesStats;
            WatchedDateAsSecs = watchedDateSecs;
            FullFileName = Repo.Instance.FileNameHash.GetByHash(Hash).FirstOrDefault()?.FileName;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_UpdateMyListFileStatus: {0}", Hash);


            try
            {
                InitProgress(progress);
                // NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
                SVR_VideoLocal vid = Repo.Instance.VideoLocal.GetByHash(Hash);
                if (vid != null)
                {
                    UpdateAndReportProgress(progress,30);
                    if (vid.GetAniDBFile() != null)
                    {
                        if (WatchedDateAsSecs > 0)
                        {
                            DateTime? watchedDate = Commons.Utils.AniDB.GetAniDBDateAsDate(WatchedDateAsSecs);
                            ShokoService.AnidbProcessor.UpdateMyListFileStatus(vid, Watched, watchedDate);
                        }
                        else
                            ShokoService.AnidbProcessor.UpdateMyListFileStatus(vid, Watched);
                    }
                    else
                    {
                        // we have a manual link, so get the xrefs and add the episodes instead as generic files
                        var xrefs = vid.EpisodeCrossRefs;
                        foreach (var xref in xrefs)
                        {
                            var episode = xref.GetEpisode();
                            if (episode == null) continue;
                            if (WatchedDateAsSecs > 0)
                            {
                                DateTime? watchedDate = Commons.Utils.AniDB.GetAniDBDateAsDate(WatchedDateAsSecs);
                                ShokoService.AnidbProcessor.UpdateMyListFileStatus(vid, episode.AnimeID,
                                    episode.EpisodeNumber, Watched, watchedDate);
                            }
                            else
                                ShokoService.AnidbProcessor.UpdateMyListFileStatus(vid, episode.AnimeID,
                                    episode.EpisodeNumber, Watched);
                        }
                    }
                    UpdateAndReportProgress(progress,60);

                    logger.Info("Updating file list status: {0} - {1}", vid, Watched);

                    if (UpdateSeriesStats)
                    {
                        // update watched stats
                        List<SVR_AnimeEpisode> eps = Repo.Instance.AnimeEpisode.GetByHash(vid.ED2KHash);
                        if (eps.Count > 0)
                        {
                            eps.DistinctBy(a => a.AnimeSeriesID).ForEach(a => a.GetAnimeSeries().QueueUpdateStats());
                        }
                    }

                }
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing Command AniDB.UpdateMyListFileStatus: {Hash} - {ex}", ex);
            }
        }
    }
}