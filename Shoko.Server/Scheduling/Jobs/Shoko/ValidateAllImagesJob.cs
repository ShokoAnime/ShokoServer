using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Scheduling.Jobs.TvDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class ValidateAllImagesJob : BaseJob
{
    private const string ScanForType = "Scanning {EntityType} for corrupted images...";
    private const string FoundCorruptedOfType = "Found {Count} corrupted {EntityType}";
    private const string CorruptImageFound = "Corrupt image found! Attempting Redownload: {FullImagePath}";
    private const string ReQueueingForDownload = "Deleting and queueing for redownload {CurrentCount}/{TotalCount}";

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
            .Where(episode => !Misc.IsImageValid(episode.GetFullImagePath()))
            .ToList();

        _logger.LogInformation(FoundCorruptedOfType, episodes.Count, episodes.Count == 1 ? "TvDB episode thumbnail" : "TvDB episode thumbnails");
        foreach (var episode in episodes)
        {
            _logger.LogTrace(CorruptImageFound, episode.GetFullImagePath());
            await RemoveImageAndQueueRedownload<DownloadTvDBImageJob>(ImageEntityType.TvDB_Episode, episode.TvDB_EpisodeID);
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
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, posters.Count, posters.Count == 1 ? "TvDB poster" : "TvDB posters");
            foreach (var poster in posters)
            {
                _logger.LogTrace(CorruptImageFound, poster.GetFullImagePath());
                await RemoveImageAndQueueRedownload<DownloadTvDBImageJob>(ImageEntityType.TvDB_Cover, poster.TvDB_ImagePosterID);
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
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, fanartList.Count, "TvDB fanart");
            foreach (var fanart in fanartList)
            {
                _logger.LogTrace(CorruptImageFound, fanart.GetFullImagePath());
                await RemoveImageAndQueueRedownload<DownloadTvDBImageJob>(ImageEntityType.TvDB_FanArt, fanart.TvDB_ImageFanartID);
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
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, wideBanners.Count, wideBanners.Count == 1 ? "TvDB wide-banner" : "TvDB wide-banners");
            foreach (var wideBanner in wideBanners)
            {
                _logger.LogTrace(CorruptImageFound, wideBanner.GetFullImagePath());
                await RemoveImageAndQueueRedownload<DownloadTvDBImageJob>(ImageEntityType.TvDB_Banner, wideBanner.TvDB_ImageWideBannerID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, wideBanners.Count);
                UpdateProgress($" - TvDB Banners - {count}/{wideBanners.Count}");
            }
        }

        if (_settings.MovieDb.AutoPosters)
        {
            count = 0;
            UpdateProgress(" - TMDB Posters");
            _logger.LogInformation(ScanForType, "TMDB posters");
            var posters = RepoFactory.MovieDB_Poster.GetAll()
                .Where(poster => !Misc.IsImageValid(poster.GetFullImagePath()))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, posters.Count, posters.Count == 1 ? "TMDB poster" : "TMDB posters");
            foreach (var poster in posters)
            {
                _logger.LogTrace(CorruptImageFound, poster.GetFullImagePath());
                await RemoveImageAndQueueRedownload<DownloadTMDBImageJob>(ImageEntityType.MovieDB_Poster, poster.MovieDB_PosterID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, posters.Count);
                UpdateProgress($" - TMDB Posters - {count}/{posters.Count}");
            }
        }

        if (_settings.MovieDb.AutoFanart)
        {
            UpdateProgress(" - TMDB Fanart");
            count = 0;
            _logger.LogInformation(ScanForType, "TMDB fanart");
            var fanartList = RepoFactory.MovieDB_Fanart.GetAll()
                .Where(fanart => !Misc.IsImageValid(fanart.GetFullImagePath()))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, fanartList.Count, "TMDB fanart");
            foreach (var fanart in fanartList)
            {
                _logger.LogTrace(CorruptImageFound, fanart.GetFullImagePath());
                await RemoveImageAndQueueRedownload<DownloadTMDBImageJob>(ImageEntityType.MovieDB_FanArt, fanart.MovieDB_FanartID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, fanartList.Count);
                UpdateProgress($" - TMDB Fanart - {count}/{fanartList.Count}");
            }
        }

        count = 0;
        UpdateProgress(" - AniDB Posters");
        _logger.LogInformation(ScanForType, "AniDB posters");
        var animeList = RepoFactory.AniDB_Anime.GetAll()
            .Where(anime => !Misc.IsImageValid(anime.PosterPath))
            .ToList();

        _logger.LogInformation(FoundCorruptedOfType, animeList.Count, animeList.Count == 1 ? "AniDB poster" : "AniDB posters");
        foreach (var anime in animeList)
        {
            _logger.LogTrace(CorruptImageFound, anime.PosterPath);
            await RemoveImageAndQueueRedownload<DownloadAniDBImageJob>(ImageEntityType.AniDB_Cover, anime.AnimeID, anime.AnimeID);
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
                .Where(character => !Misc.IsImageValid(character.GetPosterPath()))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, characters.Count, characters.Count == 1 ? "AniDB Character" : "AniDB Characters");
            foreach (var character in characters)
            {
                _logger.LogTrace(CorruptImageFound, character.GetPosterPath());
                await RemoveImageAndQueueRedownload<DownloadAniDBImageJob>(ImageEntityType.AniDB_Character, character.CharID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count, characters.Count);
                UpdateProgress($" - AniDB Characters - {count}/{characters.Count}");
            }
        }

        if (_settings.AniDb.DownloadCreators)
        {
            count = 0;
            UpdateProgress(" - AniDB Creators");
            _logger.LogInformation(ScanForType, "AniDB Seiyuu");
            var staff = RepoFactory.AniDB_Seiyuu.GetAll()
                .Where(va => !Misc.IsImageValid(va.GetPosterPath()))
                .ToList();

            _logger.LogInformation(FoundCorruptedOfType, staff.Count, "AniDB Seiyuu");
            foreach (var seiyuu in staff)
            {
                _logger.LogTrace(CorruptImageFound, seiyuu.GetPosterPath());
                await RemoveImageAndQueueRedownload<DownloadAniDBImageJob>(ImageEntityType.AniDB_Creator, seiyuu.SeiyuuID);
                if (++count % 10 != 0) continue;
                _logger.LogInformation(ReQueueingForDownload, count,
                    staff.Count);
                UpdateProgress($" - AniDB Creators - {count}/{staff.Count}");
            }
        }
    }

    private async Task RemoveImageAndQueueRedownload<T>(ImageEntityType entityTypeEnum, int entityID, int animeID = 0) where T : class, IImageDownloadJob
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<T>(
            c =>
            {
                c.Anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID)?.PreferredTitle;
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
