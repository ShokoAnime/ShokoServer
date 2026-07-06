using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
/// Scans for <see cref="StoredReleaseInfo"/> records that are
/// missing key fields (unknown source, missing audio or subtitle languages)
/// and re-queues the appropriate provider job for each file on the backoff
/// schedule defined by each provider's <c>GetRescanDelay</c> method.
/// </summary>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Import)]
public class ScanForMissingReleaseInfoJob(
        IVideoReleaseService videoReleaseService,
        StoredReleaseInfoRepository releaseInfoRepository,
        VideoLocalRepository videoLocals
) : BaseJob
{

    public override string TypeName => "Scan for Missing Release Info";

    public override string Title => "Scanning for Missing Release Info";

    public override async Task Execute()
    {
        var allReleases = releaseInfoRepository.GetAll()
            .Where(r => !r.PreventRescan)
            .ToList();

        _logger.LogInformation("Evaluating {Count} releases for possible rescan.", allReleases.Count);

        var incompleteReleases = new List<StoredReleaseInfo>();

        foreach (var release in allReleases)
        {
            var audioLangs = release.AudioLanguages;
            var subLangs = release.SubtitleLanguages;

            if (release.Source == ReleaseSource.Unknown || audioLangs is null || subLangs is null)
            {
                incompleteReleases.Add(release);
                continue;
            }

            if (audioLangs.Contains(TitleLanguage.Unknown) || subLangs.Contains(TitleLanguage.Unknown))
            {
                incompleteReleases.Add(release);
                continue;
            }

            // Flag for rescan if the file has more than 10 embedded streams of a type
            // but SRI recorded fewer than 10 languages for it — indicates truncated/stale data.
            if (audioLangs.Count >= 10 && subLangs.Count >= 10) continue;

            var video = videoLocals.GetByEd2kAndSize(release.ED2K, release.FileSize);
            var media = video?.MediaInfo;
            if (media is null) continue;

            if ((audioLangs.Count < 10 && media.AudioStreams.Count > 10) ||
                (subLangs.Count < 10 && media.TextStreams.Count(t => !t.External) > 10))
            {
                incompleteReleases.Add(release);
            }
        }

        _logger.LogInformation("Found {Count} releases with missing info to evaluate for rescan.", incompleteReleases.Count);

        var queued = 0;
        foreach (var release in incompleteReleases)
        {
            if (videoLocals.GetByEd2kAndSize(release.ED2K, release.FileSize) is not { } video)
                continue;

            if (await videoReleaseService.TryScheduleRescanForVideo(video, release))
                queued++;
        }

        _logger.LogInformation("Queued {Queued} provider rescan jobs for files with missing release info.", queued);
    }
}
