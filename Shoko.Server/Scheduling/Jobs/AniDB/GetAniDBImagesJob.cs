using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBImagesJob : BaseJob
{
    private SVR_AniDB_Anime _anime;
    private string _title;
    private readonly AniDBTitleHelper _titleHelper;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;

    public int AnimeID { get; set; }
    public bool ForceDownload { get; set; }
    public bool OnlyPosters { get; set; }

    public override string TypeName => "Get AniDB Images Data";

    public override string Title => "Getting AniDB Image Data";
    public override Dictionary<string, object> Details => _title == null
        ? new()
        {
            {
                "AnimeID", AnimeID
            }
        }
        : new()
        {
            {
                "Anime", _title
            }
        };

    public override void PostInit()
    {
        _anime = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID);
        _title = _anime?.PreferredTitle ?? _titleHelper.SearchAnimeID(AnimeID)?.PreferredTitle;
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
        if (ForceDownload || !_anime.GetImageMetadata().IsLocalAvailable)
            await scheduler.StartJob<DownloadAniDBImageJob>(
                a => (a.ImageID, a.ImageType, a.ForceDownload) = (_anime.AnimeID, ImageEntityType.Poster, ForceDownload),
                prioritize: true
            );

        if (OnlyPosters) return;
        var requests = new List<Action<DownloadAniDBImageJob>>();

        // characters
        if (settings.AniDb.DownloadCharacters)
        {
            var characters = RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
                .Select(xref => RepoFactory.AniDB_Character.GetByCharacterID(xref.CharacterID))
                .Where(a => !string.IsNullOrEmpty(a?.ImagePath))
                .DistinctBy(a => a.CharacterID)
                .ToList();
            if (characters.Count is not 0)
                requests.AddRange(characters
                    .Where(a => ForceDownload || !(a.GetImageMetadata()?.IsLocalAvailable ?? false))
                    .Select(c => new Action<DownloadAniDBImageJob>(a =>
                    {
                        a.ParentName = _title;
                        a.ImageID = c.CharacterID;
                        a.ImageType = ImageEntityType.Character;
                        a.ForceDownload = ForceDownload;
                    })));
            else
                _logger.LogWarning("No AniDB characters were found for {Anime}", _anime.PreferredTitle ?? AnimeID.ToString());
        }

        // creators
        if (settings.AniDb.DownloadCreators)
        {
            // Get all voice-actors working on this anime.
            var voiceActors = RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(AnimeID)
                .Select(xref => RepoFactory.AniDB_Creator.GetByCreatorID(xref.CreatorID))
                .Where(va => !string.IsNullOrEmpty(va?.ImagePath));
            // Get all staff members working on this anime.
            var staffMembers = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(AnimeID)
                .Select(xref => RepoFactory.AniDB_Creator.GetByCreatorID(xref.CreatorID))
                .Where(staff => !string.IsNullOrEmpty(staff?.ImagePath));
            // Concatenate the streams into a single list.
            var creators = voiceActors.Concat(staffMembers)
                .DistinctBy(creator => creator.CreatorID)
                .ToList();

            if (creators.Count is not 0)
                requests.AddRange(creators
                    .Where(a => ForceDownload || !(a.GetImageMetadata()?.IsLocalAvailable ?? false))
                    .Select(va => new Action<DownloadAniDBImageJob>(a =>
                {
                    a.ParentName = _title;
                    a.ImageID = va.CreatorID;
                    a.ImageType = ImageEntityType.Person;
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

    public GetAniDBImagesJob(AniDBTitleHelper aniDBTitleHelper, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory)
    {
        _titleHelper = aniDBTitleHelper;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
    }

    protected GetAniDBImagesJob() { }
}
