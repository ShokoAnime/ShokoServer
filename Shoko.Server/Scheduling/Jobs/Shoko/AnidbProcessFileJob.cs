using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
///   Asks AniDB for the release of a video, held to AniDB's UDP rate limit and
///   sharing its pool with the other UDP jobs.
/// </summary>
[DatabaseRequired]
[LimitConcurrency(4)]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.Import)]
[JobPriority(Default = 20, Prioritized = 70)]
public sealed class AnidbProcessFileJob(
    IVideoReleaseService videoReleaseService,
    VideoLocalRepository videoLocals,
    StoredReleaseInfo_MatchAttemptRepository matchAttempts
) : ProcessReleaseProviderJob<AnidbReleaseProvider>(videoReleaseService, videoLocals, matchAttempts);
