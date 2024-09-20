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
using Shoko.Server.Scheduling.Jobs.TvDB;
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
        UpdateProgress(" - TvDB Episodes");
        _logger.LogInformation(ScanForType, "TvDB episodes");
        var episodes = RepoFactory.TvDB_Episode.GetAll()
            .Where(episode => episode.GetImageMetadata()?.IsLocalAvailable ?? false)
            .GroupBy(episode => episode.SeriesID)
            .Select(groupBy => (Series: RepoFactory.TvDB_Series.GetByTvDBID(groupBy.Key), groupBy))
            .Where(tuple => tuple.Series is not null)
            .SelectMany(tuple => tuple.groupBy.Select(episode => (Episode: episode, tuple.Series)))
            .ToList();

        _logger.LogInformation(FoundCorruptedOfType, episodes.Count, episodes.Count == 1 ? "TvDB episode thumbnail" : "TvDB episode thumbnails");
        foreach (var (episode, series) in episodes)
        {
            _logger.LogTrace(CorruptImageFound, episode.GetFullImagePath());
            await RemoveImageAndQueueDownload<DownloadTvDBImageJob>(ImageEntityType.Thumbnail, episode.TvDB_EpisodeID, series?.SeriesName);
            if (++count % 10 != 0) continue;
            _logger.LogInformation(ReQueueingForDownload, count, episodes.Count);
            UpdateProgress($" - TvDB Episodes - {count}/{episodes.Count}");
        }

        if (_settings.TvDB.AutoPosters)
        {
            count = 0;
            UpdateProgress(" - TvDB Posters");
            _logger.LogInformation(ScanForType, "TvDB posters");
            var posters = RepoFactory.TvDB_ImagePoster.GetAll()
                .Where(poster => !Misc.IsImageValid(poster.GetFullImagePath()))
                .GroupBy(episode => episode.SeriesID)
                .Select(groupBy => (Series: RepoFactory.TvDB_Series.GetByTvDBID(groupBy.Key), groupBy))
                .Where(tuple => tuple.Series is not null)
                .SelectMany(tuple => tuple.groupBy.Select(episode => (Episode: episode, tuple.Series)))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, posters.Count, posters.Count == 1 ? "TvDB poster" : "TvDB posters");
            foreach (var (poster, series) in posters)
            {
                _logger.LogTrace(CorruptImageFound, poster.GetFullImagePath());
                await RemoveImageAndQueueDownload<DownloadTvDBImageJob>(ImageEntityType.Poster, poster.TvDB_ImagePosterID, series?.SeriesName);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, posters.Count);
                UpdateProgress($" - TvDB Posters - {count}/{posters.Count}");
            }
        }

        if (_settings.TvDB.AutoFanart)
        {
            count = 0;
            UpdateProgress(" - TvDB Fanart");
            _logger.LogInformation(ScanForType, "TvDB fanart");
            var fanartList = RepoFactory.TvDB_ImageFanart.GetAll()
                .Where(fanart => !Misc.IsImageValid(fanart.GetFullImagePath()))
                .GroupBy(episode => episode.SeriesID)
                .Select(groupBy => (Series: RepoFactory.TvDB_Series.GetByTvDBID(groupBy.Key), groupBy))
                .Where(tuple => tuple.Series is not null)
                .SelectMany(tuple => tuple.groupBy.Select(episode => (Episode: episode, tuple.Series)))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, fanartList.Count, "TvDB fanart");
            foreach (var (fanart, series) in fanartList)
            {
                _logger.LogTrace(CorruptImageFound, fanart.GetFullImagePath());
                await RemoveImageAndQueueDownload<DownloadTmdbImageJob>(ImageEntityType.Backdrop, fanart.TvDB_ImageFanartID, series?.SeriesName);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, fanartList.Count);
                UpdateProgress($" - TvDB Fanart - {count}/{fanartList.Count}");
            }
        }

        if (_settings.TvDB.AutoWideBanners)
        {
            count = 0;
            _logger.LogInformation(ScanForType, "TvDB wide-banners");
            UpdateProgress(" - TvDB Banners");
            var wideBanners = RepoFactory.TvDB_ImageWideBanner.GetAll()
                .Where(wideBanner => !Misc.IsImageValid(wideBanner.GetFullImagePath()))
                .GroupBy(episode => episode.SeriesID)
                .Select(groupBy => (Series: RepoFactory.TvDB_Series.GetByTvDBID(groupBy.Key), groupBy))
                .Where(tuple => tuple.Series is not null)
                .SelectMany(tuple => tuple.groupBy.Select(episode => (Episode: episode, tuple.Series)))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, wideBanners.Count, wideBanners.Count == 1 ? "TvDB wide-banner" : "TvDB wide-banners");
            foreach (var (wideBanner, series) in wideBanners)
            {
                _logger.LogTrace(CorruptImageFound, wideBanner.GetFullImagePath());
                await RemoveImageAndQueueDownload<DownloadTvDBImageJob>(ImageEntityType.Banner, wideBanner.TvDB_ImageWideBannerID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, wideBanners.Count);
                UpdateProgress($" - TvDB Banners - {count}/{wideBanners.Count}");
            }
        }

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
            .Where(anime => !anime.GetImageMetadata().IsLocalAvailable)
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
                .Where(character => !Misc.IsImageValid(character.GetFullImagePath()))
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
            var staff = RepoFactory.AniDB_Creator.GetAll()
                .Where(va => !Misc.IsImageValid(va.GetFullImagePath()))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, staff.Count, "AniDB Creator");
            foreach (var seiyuu in staff)
            {
                _logger.LogTrace(CorruptImageFound, seiyuu.GetFullImagePath());
                await RemoveImageAndQueueDownload<DownloadAniDBImageJob>(ImageEntityType.Person, seiyuu.CreatorID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count,
                    staff.Count);
                UpdateProgress($" - AniDB Creators - {count}/{staff.Count}");
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
