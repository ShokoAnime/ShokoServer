#pragma warning disable CS8618
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
/// Scans for <see cref="Models.Release.StoredReleaseInfo"/> records that are
/// missing key fields (unknown source, missing audio or subtitle languages)
/// and re-queues an AniDB lookup for each file on an exponential backoff
/// schedule configured via <see cref="AnidbReleaseProvider.AnidbReleaseProviderSettings"/>.
/// </summary>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Import)]
public class ScanForMissingReleaseInfoJob : BaseJob
{
    private readonly IVideoReleaseService _videoReleaseService;

    private readonly StoredReleaseInfoRepository _releaseInfoRepository;

    private readonly StoredReleaseInfo_MatchAttemptRepository _matchAttemptRepository;

    private readonly VideoLocalRepository _videoLocals;

    private readonly IQueueScheduler _scheduler;

    public override string TypeName => "Scan for Missing Release Info";

    public override string Title => "Scanning for Missing Release Info";

    public override async Task Execute()
    {
        var anidbProviderInfo = _videoReleaseService.GetProviderInfo<AnidbReleaseProvider>();

        var incompleteReleases = _releaseInfoRepository.GetAll()
            .Where(r => r.Source == ReleaseSource.Unknown || r.AudioLanguages is null || r.SubtitleLanguages is null)
            .ToList();

        _logger.LogInformation("Found {Count} releases with missing info to evaluate for rescan.", incompleteReleases.Count);

        var queued = 0;
        foreach (var release in incompleteReleases)
        {
            var matchAttempts = _matchAttemptRepository.GetByEd2kAndFileSize(release.ED2K, release.FileSize);
            var latest = matchAttempts.MaxBy(m => m.AttemptEndedAt);
            if (latest is null) continue;

            var delay = anidbProviderInfo.Provider.GetRescanDelay(release, latest);
            if (delay is null) continue;

            if (DateTime.Now < latest.AttemptEndedAt + delay.Value) continue;

            var videoLocal = _videoLocals.GetByEd2kAndSize(release.ED2K, release.FileSize);
            if (videoLocal is null) continue;

            latest.AttemptCount++;
            _matchAttemptRepository.Save(latest);

            await _scheduler.StartJob<AnidbProcessFileJob>(job =>
            {
                job.VideoLocalID = videoLocal.VideoLocalID;
                job.SkipMyList = true;
                job.MatchAttemptID = latest.StoredReleaseInfo_MatchAttemptID;
            });
            queued++;
        }

        _logger.LogInformation("Queued {Queued} AniDB rescan jobs for files with missing release info.", queued);
    }

    public ScanForMissingReleaseInfoJob(
        IVideoReleaseService videoReleaseService,
        StoredReleaseInfoRepository releaseInfoRepository,
        StoredReleaseInfo_MatchAttemptRepository matchAttemptRepository,
        VideoLocalRepository videoLocals,
        IQueueScheduler scheduler)
    {
        _videoReleaseService = videoReleaseService;
        _releaseInfoRepository = releaseInfoRepository;
        _matchAttemptRepository = matchAttemptRepository;
        _videoLocals = videoLocals;
        _scheduler = scheduler;
    }

    protected ScanForMissingReleaseInfoJob() { }
}
