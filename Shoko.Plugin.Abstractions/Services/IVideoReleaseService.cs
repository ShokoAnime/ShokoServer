using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// Video service.
/// </summary>
public interface IVideoReleaseService
{
    /// <summary>
    /// Event raised when a video release is saved to the database.
    /// </summary>
    event EventHandler<VideoReleaseEventArgs>? VideoReleaseSaved;

    /// <summary>
    /// Event raised when a video release is deleted from the database.
    /// </summary>
    event EventHandler<VideoReleaseEventArgs>? VideoReleaseDeleted;

    /// <summary>
    /// Event raised when the release info providers are updated.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Gets or sets a value indicating whether to use parallel mode.
    /// </summary>
    /// <remarks>
    ///   Parallel mode affects <see cref="FindReleaseForVideo(IVideo, bool, CancellationToken)"/>
    ///   and makes it run all providers in parallel and pick the highest priority valid result,
    ///   as opposed to running each provider serially in the priority order and picking the first
    ///   valid result when parallel mode is off.
    /// </remarks>
    bool ParallelMode { get; set; }

    /// <summary>
    ///   Adds the release info providers.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service. Calling it
    ///   multiple times will have no effect.
    /// </remarks>
    /// <param name="providers">
    ///   The release info providers.
    /// </param>
    void AddProviders(IEnumerable<IReleaseInfoProvider> providers);

    /// <summary>
    ///   List out all available providers, if they're enabled for use in
    ///   <see cref="FindReleaseForVideo(IVideo, bool, CancellationToken)"/> and
    ///   their priority order when used in said method.
    /// </summary>
    /// <returns>
    ///   An enumerable of <see cref="ReleaseInfoProviderInfo"/>s, one for each
    ///   available <see cref="IReleaseInfoProvider"/>.
    /// </returns>
    IEnumerable<ReleaseInfoProviderInfo> GetAvailableProviders();

    /// <summary>
    ///   Edit the settings for an <see cref="IReleaseInfoProvider"/>, such as
    ///   whether it's enabled for automatic usage, and the priority during
    ///   automatic usage.
    /// </summary>
    /// <param name="providers">
    ///   The providers to update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="providers"/> is <c>null</c>.
    /// </exception>
    void UpdateProviders(params ReleaseInfoProviderInfo[] providers);

    /// <summary>
    ///   Gets the <see cref="IReleaseInfoProvider"/> with the specified ID.
    /// </summary>
    /// <param name="providerID">
    ///   The ID of the provider.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="providerID"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The provider, or <c>null</c> if none could be found.
    /// </returns>
    ReleaseInfoProviderInfo? GetProviderByID(Guid providerID);

    /// <summary>
    ///   Get the current release for the specified video, if one exists.
    /// </summary>
    /// <param name="video">
    ///   The video to find a release for.
    /// </param>
    /// <returns>
    ///   The found release, or <c>null</c> if none could be found.
    /// </returns>
    IReleaseInfo? GetCurrentReleaseForVideo(IVideo video);

    /// <summary>
    ///   If parallel mode is disabled, then it will run all enabled
    ///   <see cref="IReleaseInfoProvider"/>s, in priority order, until a
    ///   release is found or all providers are exhausted. If parallel mode is
    ///   enabled, then it will run all enabled <see cref="IReleaseInfoProvider"/>s
    ///   in parallel and pick the highest priority valid result.
    /// </summary>
    /// <remarks>
    ///   This method does not save the found release to the database unless
    ///   <paramref name="saveRelease"/> is set to <c>true</c>.
    /// </remarks>
    /// <param name="video">
    ///   The video to find a release for.
    /// </param>
    /// <param name="saveRelease">
    ///   If not set to <c>true</c>, then the found release will not be saved,
    ///   allowing the user to preview the release before saving it using
    ///   <see cref="SaveReleaseForVideo(IVideo, IReleaseInfo)"/>, or discarding
    ///   it.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the search.
    /// </param>
    /// <returns>
    ///   The found release, or <c>null</c> if none could be found.
    /// </returns>
    Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, bool saveRelease = true, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Saves the release info for the specified video in the database, and
    ///   returns the saved release info. This will overwrite any existing
    ///   release for the video.
    /// </summary>
    /// <param name="video">
    ///   The video to save the release for.
    /// </param>
    /// <param name="release">
    ///   The release details to save.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Release does not have at least one cross reference or have invalid cross references.
    /// </exception>
    /// <returns>
    ///   The saved release.
    /// </returns>
    Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release);

    /// <summary>
    /// Saves the release info for the specified video in the database, and
    /// returns the saved release info. This will overwrite any existing
    /// release for the video.
    /// </summary>
    /// <param name="video">
    ///   The video to save the release for.
    /// </param>
    /// <param name="release">
    ///   The release details to save.
    /// </param>
    /// <param name="providerName">
    ///   Optional. Set the name of the provider.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Release does not have at least one cross reference or have invalid cross references.
    /// </exception>
    /// <returns>
    ///   The saved release.
    /// </returns>
    Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, ReleaseInfo release, string providerName = "User");

    /// <summary>
    /// Clears the current release for the specified video.
    /// </summary>
    /// <param name="video">
    ///   The video to clear the current release for.
    /// </param>
    /// <returns>
    ///   A task that represents the asynchronous operation.
    /// </returns>
    Task ClearReleaseForVideo(IVideo video);
}
