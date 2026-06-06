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
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
/// Scans for <see cref="Models.Release.StoredReleaseInfo"/> records that are
/// missing key fields (unknown source, missing audio or subtitle languages)
/// and re-queues the appropriate provider job for each file on the backoff
/// schedule defined by each provider's <c>GetRescanDelay</c> method.
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

    public override string TypeName => "Scan for Missing Release Info";

    public override string Title => "Scanning for Missing Release Info";

    public override async Task Execute()
    {
        var incompleteReleases = _releaseInfoRepository.GetAll()
            .Where(r => r.Source == ReleaseSource.Unknown || r.AudioLanguages is null || r.SubtitleLanguages is null)
            .ToList();

        _logger.LogInformation("Found {Count} releases with missing info to evaluate for rescan.", incompleteReleases.Count);

        var queued = 0;
        foreach (var release in incompleteReleases)
        {
            var matchAttempts = _matchAttemptRepository.GetByEd2kAndFileSize(release.ED2K, release.FileSize);
            var latest = matchAttempts.MaxBy(m => m.AttemptEndedAt);
            if (latest is null)
            {
                latest = new StoredReleaseInfo_MatchAttempt
                {
                    ED2K = release.ED2K,
                    FileSize = release.FileSize,
                    ProviderName = release.ProviderName,
                    AttemptStartedAt = DateTime.UnixEpoch,
                    AttemptEndedAt = DateTime.UnixEpoch,
                    AttemptCount = 1,
                    AttemptedProviderNames = [release.ProviderName],
                };
                _matchAttemptRepository.Save(latest);
            }

            var videoLocal = _videoLocals.GetByEd2kAndSize(release.ED2K, release.FileSize);
            if (videoLocal is null) continue;

            if (await _videoReleaseService.TryScheduleRescanForVideo(videoLocal, release, latest))
                queued++;
        }

        _logger.LogInformation("Queued {Queued} provider rescan jobs for files with missing release info.", queued);
    }

    public ScanForMissingReleaseInfoJob(
        IVideoReleaseService videoReleaseService,
        StoredReleaseInfoRepository releaseInfoRepository,
        StoredReleaseInfo_MatchAttemptRepository matchAttemptRepository,
        VideoLocalRepository videoLocals)
    {
        _videoReleaseService = videoReleaseService;
        _releaseInfoRepository = releaseInfoRepository;
        _matchAttemptRepository = matchAttemptRepository;
        _videoLocals = videoLocals;
    }

    protected ScanForMissingReleaseInfoJob() { }
}
