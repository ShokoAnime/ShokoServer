using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class ManualLinkJob : BaseJob
{
    private readonly IServerSettings _settings;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly VideoLocal_PlaceService _vlPlaceService;

    public int VideoLocalID { get; set; }
    public int EpisodeID { get; set; }
    public int Percentage { get; set; }
    
    private SVR_AnimeEpisode _episode;
    private SVR_VideoLocal _vlocal;

    public override string Name => "Manually Link File";
    public override QueueStateStruct Description
    {
        get
        {
            if (_vlocal != null && _episode != null)
            {
                return new QueueStateStruct
                {
                    message = "Linking File: {0} to Episode: {1}",
                    queueState = QueueStateEnum.LinkFileManually,
                    extraParams = new[] { _vlocal.FileName, _episode.Title }
                };
            }

            return new QueueStateStruct
            {
                message = "Linking File: {0} to Episode: {1}",
                queueState = QueueStateEnum.LinkFileManually,
                extraParams = new[] { VideoLocalID.ToString(), EpisodeID.ToString() }
            };
        }
    }

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        _episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID);
    }

    public override async Task Process()
    {
        // The flow has changed.
        // Check for previous existence, merge info if needed
        // If it's a new file or info is missing, queue a hash
        // HashFileJob will create the records for a new file, so don't save an empty record.
        _logger.LogInformation("Processing {Job}: {VideoLocal} | {EpisodeID}", nameof(ManualLinkJob), VideoLocalID, EpisodeID);

        var xref = new CrossRef_File_Episode
        {
            Hash = _vlocal.ED2KHash,
            FileName = _vlocal.FileName,
            FileSize = _vlocal.FileSize,
            CrossRefSource = (int)CrossRefSource.User,
            AnimeID = _episode.AniDB_Episode.AnimeID,
            EpisodeID = _episode.AniDB_EpisodeID,
            Percentage = Percentage is > 0 and <= 100 ? Percentage : 100,
            EpisodeOrder = 1
        };

        RepoFactory.CrossRef_File_Episode.Save(xref);

        await ProcessFileQualityFilter();

        _vlocal.Places.ForEach(a => { _vlPlaceService.RenameAndMoveAsRequired(a); });

        // Set the import date.
        _vlocal.DateTimeImported = DateTime.Now;
        RepoFactory.VideoLocal.Save(_vlocal);

        var ser = _episode.GetAnimeSeries();
        ser.EpisodeAddedDate = DateTime.Now;
        RepoFactory.AnimeSeries.Save(ser, false, true);

        //Update will re-save
        ser.QueueUpdateStats();

        foreach (var grp in ser.AllGroupsAbove)
        {
            grp.EpisodeAddedDate = DateTime.Now;
            RepoFactory.AnimeGroup.Save(grp, false, false);
        }

        ShokoEventHandler.Instance.OnFileMatched(_vlocal.GetBestVideoLocalPlace(), _vlocal);

        if (_settings.AniDb.MyList_AddFiles)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.StartJob<AddFileToMyListJob>(c => c.Hash = _vlocal.Hash);
        }
    }

    private async Task ProcessFileQualityFilter()
    {
        if (!_settings.FileQualityFilterEnabled) return;

        var videoLocals = _episode.GetVideoLocals();
        if (videoLocals == null) return;

        videoLocals.Sort(FileQualityFilter.CompareTo);
        var keep = videoLocals.Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep).ToList();
        foreach (var vl2 in keep) videoLocals.Remove(vl2);

        if (videoLocals.Contains(_vlocal)) videoLocals.Remove(_vlocal);

        videoLocals = videoLocals.Where(FileQualityFilter.CheckFileKeep).ToList();

        foreach (var toDelete in videoLocals.SelectMany(a => a.Places)) await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(toDelete);
    }

    public ManualLinkJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, VideoLocal_PlaceService vlPlaceService)
    {
        _settings = settingsProvider.GetSettings();
        _schedulerFactory = schedulerFactory;
        _vlPlaceService = vlPlaceService;
    }

    protected ManualLinkJob() { }
}
