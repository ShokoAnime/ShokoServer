using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBImagesJob : BaseJob
{
    private SVR_AniDB_Anime _anime;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;

    public int AnimeID { get; set; }
    public bool ForceDownload { get; set; }

    public override string Name => "Get AniDB Images Data";
    public override QueueStateStruct Description => new()
    {
        message = "Getting Images for {0}",
        queueState = QueueStateEnum.DownloadImage,
        extraParams = new[]
        {
            _anime?.PreferredTitle ?? AnimeID.ToString()
        }
    };

    public override void PostInit()
    {
        _anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
    }

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(GetAniDBImagesJob), _anime?.PreferredTitle ?? AnimeID.ToString());
        if (_anime == null)
        {
            _logger.LogWarning("{Anime} was null for {AnimeID}", nameof(_anime), AnimeID);
            return;
        }

        var settings = _settingsProvider.GetSettings();

        // cover
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJobNow<DownloadAniDBImageJob>(a =>
        {
            a.AnimeID = _anime.AnimeID;
            a.ImageType = ImageEntityType.AniDB_Cover;
            a.ForceDownload = ForceDownload;
        });
        var requests = new List<Action<DownloadAniDBImageJob>>();

        // characters
        if (settings.AniDb.DownloadCharacters)
        {
            var characters = RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
                .Select(xref => RepoFactory.AniDB_Character.GetByCharID(xref.CharID))
                .Where(a => !string.IsNullOrEmpty(a?.PicName))
                .DistinctBy(a => a.CharID)
                .ToList();
            if (characters.Any())
                requests.AddRange(characters.Select(c => new Action<DownloadAniDBImageJob>(a =>
                {
                    a.AnimeID = _anime.AnimeID;
                    a.ImageID = c.CharID;
                    a.ImageType = ImageEntityType.AniDB_Character;
                    a.ForceDownload = ForceDownload;
                })));
            else
                _logger.LogWarning("No AniDB characters were found for {Anime}", _anime.PreferredTitle ?? AnimeID.ToString());
        }

        // creators
        if (settings.AniDb.DownloadCreators)
        {
            // Get all voice-actors working on this anime.
            var voiceActors = RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
                .SelectMany(xref => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(xref.CharID))
                .Select(xref => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(xref.SeiyuuID))
                .Where(va => !string.IsNullOrEmpty(va?.PicName));
            // Get all staff members working on this anime.
            var staffMembers = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(AnimeID)
                .Select(xref => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(xref.CreatorID))
                .Where(staff => !string.IsNullOrEmpty(staff?.PicName));
            // Concatenate the streams into a single list.
            var creators = voiceActors
                .Concat(staffMembers)
                .DistinctBy(creator => creator.SeiyuuID)
                .ToList();

            if (creators.Any())
                requests.AddRange(creators.Select(va => new Action<DownloadAniDBImageJob>(a =>
                {
                    a.AnimeID = _anime.AnimeID;
                    a.ImageID = va.SeiyuuID;
                    a.ImageType = ImageEntityType.AniDB_Creator;
                    a.ForceDownload = ForceDownload;
                })));
            else
                _logger.LogWarning("No AniDB creators were found for {Anime}", _anime.PreferredTitle ?? AnimeID.ToString());
        }

        foreach (var action in requests)
        {
            await scheduler.StartJob(action);
        }
    }

    public GetAniDBImagesJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory)
    {
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
    }

    protected GetAniDBImagesJob()
    {
    }
}
