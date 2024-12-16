using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Utils;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Settings;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class ValidateAllImagesJob : BaseJob
{
    private const string ScanForType = "Scanning {EntityType} for corrupted images...";
    private const string FoundCorruptedOfType = "Found {Count} corrupted {EntityType}";
    private const string CorruptImageFound = "Corrupt image found! Attempting re-download: {FullImagePath}";
    private const string ReQueueingForDownload = "Deleting and queueing for re-download {CurrentCount}/{TotalCount}";

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly IServerSettings _settings;

    public override string TypeName => "Validate All Images";

    public override string Title => "Validating All Images";

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(ValidateAllImagesJob));

        var count = 0;
        List<(ImageEntityType, bool)> tmdbTypes = [
            (ImageEntityType.Poster, _settings.TMDB.AutoDownloadPosters),
            (ImageEntityType.Backdrop, _settings.TMDB.AutoDownloadBackdrops),
            (ImageEntityType.Person, _settings.TMDB.AutoDownloadStaffImages),
            (ImageEntityType.Logo, _settings.TMDB.AutoDownloadLogos),
            (ImageEntityType.Art, _settings.TMDB.AutoDownloadStudioImages),
            (ImageEntityType.Thumbnail, _settings.TMDB.AutoDownloadThumbnails),
        ];
        foreach (var (imageType, enabled) in tmdbTypes)
        {
            if (!enabled) continue;
            var pluralUpper = $"TMDB {(imageType is ImageEntityType.Person ? "People" : imageType.ToString())}";
            var pluralLower = $"TMDB {pluralUpper[5..].ToLowerInvariant()}";
            var singularLower = $"TMDB {imageType.ToString().ToLowerInvariant()}";
            count = 0;
            UpdateProgress($" - {pluralUpper}");
            _logger.LogInformation(ScanForType, pluralLower);
            var images = RepoFactory.TMDB_Image.GetByType(imageType)
                .Where(image => !image.IsLocalAvailable)
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, images.Count, images.Count == 1 ? singularLower : pluralLower);
            foreach (var image in images)
            {
                _logger.LogTrace(CorruptImageFound, image.LocalPath);
                await RemoveImageAndQueueDownload<DownloadTmdbImageJob>(image.ImageType, image.TMDB_ImageID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, images.Count);
                UpdateProgress($" - {pluralUpper} - {count}/{images.Count}");
            }
        }

        count = 0;
        UpdateProgress(" - AniDB Posters");
        _logger.LogInformation(ScanForType, "AniDB posters");
        var animeList = RepoFactory.AniDB_Anime.GetAll()
            .Where(anime => !string.IsNullOrEmpty(anime.Picname) && !anime.GetImageMetadata().IsLocalAvailable)
            .ToList();

        _logger.LogInformation(FoundCorruptedOfType, animeList.Count, animeList.Count == 1 ? "AniDB poster" : "AniDB posters");
        foreach (var anime in animeList)
        {
            _logger.LogTrace(CorruptImageFound, anime.PosterPath);
            await RemoveImageAndQueueDownload<DownloadAniDBImageJob>(ImageEntityType.Poster, anime.AnimeID, anime.MainTitle);
            if (++count % 10 != 0) continue;
            _logger.LogInformation(ReQueueingForDownload, count, animeList.Count);
            UpdateProgress($" - AniDB Posters - {count}/{animeList.Count}");
        }

        if (_settings.AniDb.DownloadCharacters)
        {
            count = 0;
            UpdateProgress(" - AniDB Characters");
            _logger.LogInformation(ScanForType, "AniDB characters");
            var characters = RepoFactory.AniDB_Character.GetAll()
                .Where(character => !(character.GetImageMetadata()?.IsLocalAvailable ?? true))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, characters.Count, characters.Count == 1 ? "AniDB Character" : "AniDB Characters");
            foreach (var character in characters)
            {
                _logger.LogTrace(CorruptImageFound, character.GetFullImagePath());
                await RemoveImageAndQueueDownload<DownloadAniDBImageJob>(ImageEntityType.Character, character.CharID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, characters.Count);
                UpdateProgress($" - AniDB Characters - {count}/{characters.Count}");
            }
        }

        if (_settings.AniDb.DownloadCreators)
        {
            count = 0;
            UpdateProgress(" - AniDB Creators");
            _logger.LogInformation(ScanForType, "AniDB Creator");
            var creators = RepoFactory.AniDB_Creator.GetAll()
                .Where(creator => !(creator.GetImageMetadata()?.IsLocalAvailable ?? true))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, creators.Count, "AniDB Creator");
            foreach (var creator in creators)
            {
                _logger.LogTrace(CorruptImageFound, creator.GetFullImagePath());
                await RemoveImageAndQueueDownload<DownloadAniDBImageJob>(ImageEntityType.Person, creator.CreatorID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, creators.Count);
                UpdateProgress($" - AniDB Creators - {count}/{creators.Count}");
            }
        }
    }

    private async Task RemoveImageAndQueueDownload<T>(ImageEntityType entityTypeEnum, int entityID, string? parentName = null) where T : class, IImageDownloadJob
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<T>(
            c =>
            {
                c.ParentName = parentName;
                c.ImageID = entityID;
                c.ImageType = entityTypeEnum;
                c.ForceDownload = true;
            }
        );
    }

    // ReSharper disable once UnusedParameter.Local
    private void UpdateProgress(string progressText)
    {
        // TODO maybe make progress callbacks a thing. Ideally, we just won't have any long running tasks, but some will just be that way
        /*if (Processor == null)
            return;

        var desc = PrettyDescription;
        desc.extraParams = new[] { progressText };
        Processor.QueueState = desc;*/
    }

    public ValidateAllImagesJob(ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _schedulerFactory = schedulerFactory;
        _settings = settingsProvider.GetSettings();
    }

    protected ValidateAllImagesJob() { }
}
