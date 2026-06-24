using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Video.Events;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   Base interface for all video release information providers to implement.
/// </summary>
public interface IReleaseInfoProvider
{
    /// <summary>
    ///   Friendly name of the release information provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the release information provider.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Version of the release information provider.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   Gets the release information for the specified video, if available
    ///   from the provider.
    /// </summary>
    /// <param name="context">
    ///   The release info request with the video to get release info for.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the search.
    /// </param>
    /// <returns>
    ///   The release information, or <c>null</c> if not available.
    /// </returns>
    Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoContext context, CancellationToken cancellationToken);

    /// <summary>
    ///   Gets the release information by a specified release id, if the
    ///   provider supports it.
    /// </summary>
    /// <param name="releaseId">
    ///   The release id.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the search.
    /// </param>
    /// <returns>
    ///   The release information, or <c>null</c> if not available.
    /// </returns>
    Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken);

    /// <summary>
    ///   Called when a release was previously matched but the stored release
    ///   info may be incomplete. Returns the delay before this provider should
    ///   be called again to fill in missing fields, or <c>null</c> if no
    ///   further rescans should be scheduled — either because the info is
    ///   already complete, this provider did not originally match the file, or
    ///   the maximum number of rescan attempts has been reached.
    /// </summary>
    TimeSpan? GetRescanDelay(IReleaseInfo existingInfo, IReleaseMatchAttempt lastAttempt)
        => null;

    /// <summary>
    ///   Called before a release is saved. Allows the provider to resolve
    ///   missing data in the release info in place (e.g. look up episode-to-anime
    ///   ID mappings, fetch or validate group names). Called only on the provider
    ///   that matched the release.
    /// </summary>
    Task PrepareForSave(IVideo video, ReleaseInfo releaseInfo)
        => Task.CompletedTask;

    /// <summary>
    ///   Called after a release is saved. Use this for provider-specific
    ///   post-save actions (e.g. scheduling metadata downloads).
    ///   Called only on the provider that matched the release.
    /// </summary>
    Task OnReleaseSaved(IVideo? video, IReleaseInfo savedRelease, IReadOnlyList<IVideoCrossReference> xrefs)
        => Task.CompletedTask;

    /// <summary>
    ///   Called when a release is cleared from the database. The
    ///   <paramref name="replacingRelease"/> parameter is set when the clear
    ///   is part of a replace operation. Called only on the provider that
    ///   originally matched the cleared release.
    /// </summary>
    Task OnReleaseCleared(IVideo? video, IReleaseInfo clearedRelease, IReleaseInfo? replacingRelease)
        => Task.CompletedTask;

    /// <summary>
    ///   Called after a search completes for a video that this provider
    ///   successfully matched. Only invoked on the winning provider.
    ///   Use this to trigger provider-specific post-import actions.
    ///   Implementations should short-circuit when
    ///   <see cref="VideoReleaseSearchCompletedEventArgs.IsCancelled"/> is true.
    /// </summary>
    Task OnSearchCompleted(VideoReleaseSearchCompletedEventArgs args)
        => Task.CompletedTask;
}

/// <summary>
///   Indicates that the release information provider supports configuration,
///   and which configuration type to display in the UI.
/// </summary>
/// <typeparam name="TConfiguration">
///   The hash provider configuration type.
/// </typeparam>
public interface IReleaseInfoProvider<TConfiguration> : IReleaseInfoProvider where TConfiguration : IReleaseInfoProviderConfiguration { }
