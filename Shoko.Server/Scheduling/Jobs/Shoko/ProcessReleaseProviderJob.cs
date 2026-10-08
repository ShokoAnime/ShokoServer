using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
///   Asks one release provider for the release of a video, as one step of a
///   release search chain.
/// </summary>
/// <remarks>
///   One job type per provider, so each can be held back and limited alone. A
///   provider without a job class of its own runs through this one; a job
///   class of its own derives from this one to change its attributes.
/// </remarks>
/// <typeparam name="TProvider">The provider to ask.</typeparam>
[DatabaseRequired]
[NetworkRequired]
[ProviderJob]
[JobKeyGroup(JobKeyGroup.Import)]
[JobPriority(Default = 20, Prioritized = 70)]
public class ProcessReleaseProviderJob<TProvider>(
    IVideoReleaseService videoReleaseService,
    VideoLocalRepository videoLocals,
    StoredReleaseInfo_MatchAttemptRepository matchAttempts
) : BaseJob, IVideoReleaseProviderJob<TProvider> where TProvider : class, IReleaseInfoProvider
{
    #region Properties

    private readonly VideoReleaseService _videoReleaseService = (VideoReleaseService)videoReleaseService;

    private VideoLocal? _vlocal;

    private StoredReleaseInfo_MatchAttempt? _matchAttempt;

    private ReleaseProviderInfo? _providerInfo;

    private int? _attemptNumber;

    private string? _fileName;

    /// <summary>
    ///   The video to find the release of.
    /// </summary>
    public int VideoLocalID { get; set; }

    /// <summary>
    ///   Whether saving a found release skips the events and the MyList update.
    /// </summary>
    public bool SkipEvents { get; set; }

    /// <summary>
    ///   The match attempt the chain records its outcome on.
    /// </summary>
    public int MatchAttemptID { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Get Release Information for Video From Provider";

    /// <inheritdoc />
    public override string Title => "Getting Release Information for Video From Provider";

    /// <inheritdoc />
    public override Dictionary<string, object> Details
    {
        get
        {
            var providerName = _providerInfo?.Name ?? typeof(TProvider).Name;
            var result = new Dictionary<string, object>
            {
                ["Provider"] = providerName,
            };
            if (_attemptNumber.HasValue)
            {
                result["Attempt Number"] = _attemptNumber;
                result["Attempt Chain Index"] = _matchAttempt!.AttemptedProviderNames.IndexOf(providerName);
            }
            if (string.IsNullOrEmpty(_fileName))
                result["Video"] = VideoLocalID;
            else
                result["File Path"] = _fileName;
            if (!SkipEvents) result["Add to MyList"] = true;
            return result;
        }
    }

    #endregion

    #region Execution

    /// <inheritdoc />
    public override void PostInit()
    {
        _vlocal = videoLocals.GetByID(VideoLocalID);
        _matchAttempt = matchAttempts.GetByID(MatchAttemptID);
        if (_vlocal is not null && _matchAttempt is not null)
            _attemptNumber = matchAttempts.GetByEd2kAndFileSize(_vlocal.Hash, _vlocal.FileSize)
                .FindIndex(m => m.StoredReleaseInfo_MatchAttemptID == MatchAttemptID) + 1;
        _providerInfo = FindProviderInfo();
        _fileName = VideoService.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    /// <inheritdoc />
    public override async Task Execute()
    {
        _logger.LogInformation(
            "Processing {Job}: {FileName}",
            JobTypeNames.Short(GetType()),
            _fileName ?? VideoLocalID.ToString()
        );

        _vlocal ??= videoLocals.GetByID(VideoLocalID);
        _matchAttempt ??= matchAttempts.GetByID(MatchAttemptID);
        if (_vlocal is null || _matchAttempt is null) return;

        // The release service's own instance, which keeps the provider's memory cache.
        _providerInfo ??= FindProviderInfo();
        if (_providerInfo is null)
        {
            _logger.LogWarning("Release provider {Provider} is not registered, skipping.", typeof(TProvider).FullName);
            return;
        }

        // Skip if the chain already has a definitive (non-deferred) result.
        if (_matchAttempt.IsCompleted)
        {
            if (_matchAttempt.ProviderID.HasValue)
                _logger.LogTrace("Release already found for {FileName}, skipping provider {ProviderName}.", _fileName, _providerInfo.Name);
            return;
        }

        try
        {
            var request = new ReleaseInfoContext { Video = _vlocal, IsAutomatic = true };
            var release = await _providerInfo.Provider.GetReleaseInfoForVideo(request, CancellationToken.None);
            if (release is null || release.CrossReferences.Count < 1)
            {
                _logger.LogTrace("No release found for {FileName} via provider {ProviderName}.", _fileName, _providerInfo.Name);
                return;
            }

            var releaseInfo = new ReleaseInfoWithProvider(release, _providerInfo.Name);
            _matchAttempt.ProviderID = _providerInfo.ID;
            _matchAttempt.ProviderName = _providerInfo.Name;
            await _videoReleaseService.SaveReleaseForVideo(_vlocal, releaseInfo, matchAttempt: _matchAttempt, skipEvents: SkipEvents);
        }
        catch (Exception ex)
        {
            _matchAttempt.AttemptEndedAt = DateTime.Now;
            matchAttempts.Save(_matchAttempt);
            await _videoReleaseService.FireSearchCompleted(_vlocal, _matchAttempt, null, ex);
            throw new ChainAbortException(ex);
        }
    }

    private ReleaseProviderInfo? FindProviderInfo()
        => _videoReleaseService.GetAvailableProviders().FirstOrDefault(info => info.Provider.GetType() == typeof(TProvider));

    #endregion
}
