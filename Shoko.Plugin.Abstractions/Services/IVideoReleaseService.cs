using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// Service responsible for managing release info for videos.
/// </summary>
/// <remarks>
///   The service can operate in sequential mode or parallel model. Parallel
///   mode affects <see cref="FindReleaseForVideo(IVideo, bool, bool, CancellationToken)"/>
///   and makes it run all providers in parallel and pick the highest priority
///   valid result, as opposed to running each provider serially in the priority
///   order and picking the first valid result when running in sequential mode.
/// </remarks>
public interface IVideoReleaseService
{
    /// <summary>
    ///   Event raised when a video release is saved to the database.
    /// </summary>
    event EventHandler<VideoReleaseEventArgs>? ReleaseSaved;

    /// <summary>
    ///   Event raised when a video release is deleted from the database.
    /// </summary>
    event EventHandler<VideoReleaseEventArgs>? ReleaseDeleted;

    /// <summary>
    ///   Event raised when an automatic video release search is started.
    /// </summary>
    event EventHandler<VideoReleaseSearchStartedEventArgs>? SearchStarted;

    /// <summary>
    ///   Event raised when an automatic video release search is completed.
    /// </summary>
    event EventHandler<VideoReleaseSearchCompletedEventArgs>? SearchCompleted;

    /// <summary>
    ///   Event raised when the enabled release info providers are updated or
    ///   parallel mode is changed.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Event raised when all release info providers are registered and the
    ///   service is ready for use.
    /// </summary>
    event EventHandler? Ready;

    /// <summary>
    ///   Gets a value indicating whether any release info providers are enabled
    ///   for auto-matching.
    /// </summary>
    bool AutoMatchEnabled { get; }

    /// <summary>
    ///   Gets or sets a value indicating whether to use parallel mode.
    /// </summary>
    /// <remarks>
    ///   Parallel mode affects <see cref="FindReleaseForVideo(IVideo, bool, bool, CancellationToken)"/>
    ///   and makes it run all providers in parallel and pick the highest
    ///   priority valid result, as opposed to running each provider serially in
    ///   the priority order and picking the first valid result when running in
    ///   sequential mode.
    /// </remarks>
    bool ParallelMode { get; set; }

    /// <summary>
    ///   Adds the release info providers.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service, and will be
    ///   called during start-up. Calling it multiple times will have no effect.
    /// </remarks>
    /// <param name="providers">
    ///   The release info providers.
    /// </param>
    void AddParts(IEnumerable<IReleaseInfoProvider> providers);

    /// <summary>
    ///   List out all available providers, if they're enabled for use in
    ///   <see cref="FindReleaseForVideo(IVideo, bool, bool, CancellationToken)"/> and
    ///   their priority order when used in said method.
    /// </summary>
    /// <param name="onlyEnabled">
    ///   If true, only providers that are enabled for use in
    ///   <see cref="FindReleaseForVideo(IVideo, bool, bool, CancellationToken)"/> will
    ///   be returned.
    /// </param>
    /// <returns>
    ///   An enumerable of <see cref="ReleaseInfoProviderInfo"/>s, one for each
    ///   available <see cref="IReleaseInfoProvider"/>.
    /// </returns>
    IEnumerable<ReleaseInfoProviderInfo> GetAvailableProviders(bool onlyEnabled = false);

    /// <summary>
    ///   Edit the settings for an <see cref="IReleaseInfoProvider"/>, such as
    ///   whether it's enabled for automatic usage, and the priority during
    ///   automatic usage.
    /// </summary>
    /// <param name="providers">
    ///   The providers to update.
    /// </param>
    void UpdateProviders(params ReleaseInfoProviderInfo[] providers);

    /// <summary>
    ///   Gets the <see cref="ReleaseInfoProviderInfo"/> for a given plugin.
    /// </summary>
    /// <param name="plugin">
    ///   The plugin.
    /// </param>
    /// <returns>
    ///   The provider info.
    /// </returns>
    IReadOnlyList<ReleaseInfoProviderInfo> GetProviderInfo(IPlugin plugin);

    /// <summary>
    ///   Gets the <see cref="ReleaseInfoProviderInfo"/> for the specified ID.
    /// </summary>
    /// <param name="providerID">
    ///   The ID of the provider.
    /// </param>
    /// <returns>
    ///   The provider info, or <c>null</c> if none could be found.
    /// </returns>
    ReleaseInfoProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Gets the <see cref="ReleaseInfoProviderInfo"/> for the provider.
    /// </summary>
    /// <param name="provider">
    ///   The provider.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Providers have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is unregistered.
    /// </exception>
    /// <returns>
    ///   The provider info.
    /// </returns>
    ReleaseInfoProviderInfo GetProviderInfo(IReleaseInfoProvider provider);

    /// <summary>
    ///   Gets the <see cref="ReleaseInfoProviderInfo"/> for the specified type.
    /// </summary>
    /// <typeparam name="TProvider">
    ///   The provider type.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    ///   Providers have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <typeparamref name="TProvider"/> is unregistered.
    /// </exception>
    /// <returns>
    ///   The provider info.
    /// </returns>
    ReleaseInfoProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IReleaseInfoProvider;

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
    ///   Schedule a call to find a release for the specified video in the queue.
    /// </summary>
    /// <param name="video">
    ///   The video to find a release for.
    /// </param>
    /// <param name="force">
    ///   If set to <c>false</c>, then it will only schedule the job if the video
    ///   doesn't already have a release associated with it.
    /// </param>
    /// <param name="addToMylist">
    ///   Optional. Set to <c>false</c> to not add the release to the user's MyList if a
    ///   release is found and saved.
    /// </param>
    /// <param name="prioritize">
    ///   If set to <c>true</c>, then this video will be given higher than
    ///   default priority in the queue.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of scheduling
    ///   the job in the queue.
    /// </returns>
    Task ScheduleFindReleaseForVideo(IVideo video, bool force = false, bool addToMylist = true, bool prioritize = false);

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
    ///   <see cref="SaveReleaseForVideo(IVideo, IReleaseInfo, bool)"/>, or discarding
    ///   it.
    /// </param>
    /// <param name="addToMylist">
    ///   Optional. Set to <c>false</c> to not add the release to the user's MyList if a
    ///   release is found and saved.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the search.
    /// </param>
    /// <returns>
    ///   The found release, or <c>null</c> if none could be found.
    /// </returns>
    Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, bool saveRelease = true, bool addToMylist = true, CancellationToken cancellationToken = default);

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
    /// <param name="addToMylist">
    ///   Optional. Set to <c>false</c> to not add the release to the user's MyList.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Release does not have at least one cross reference or have invalid cross references.
    /// </exception>
    /// <returns>
    ///   The saved release.
    /// </returns>
    Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release, bool addToMylist = true);

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
    /// <param name="addToMylist">
    ///   Optional. Set to <c>false</c> to not add the release to the user's MyList.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Release does not have at least one cross reference or have invalid cross references.
    /// </exception>
    /// <returns>
    ///   The saved release.
    /// </returns>
    Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, ReleaseInfo release, string providerName = "User", bool addToMylist = true);

    /// <summary>
    /// Clears the current release for the specified video.
    /// </summary>
    /// <param name="video">
    ///   The video to clear the current release for.
    /// </param>
    /// <param name="removeFromMylist">
    ///   Optional. Set to <c>false</c> to not remove the release from the user's MyList.
    /// </param>
    /// <returns>
    ///   A task that represents the asynchronous operation.
    /// </returns>
    Task ClearReleaseForVideo(IVideo video, bool removeFromMylist = true);

    /// <summary>
    ///   Gets the release match attempts for the specified video.
    /// </summary>
    /// <param name="video">
    ///   The video to get the release match attempts for.
    /// </param>
    /// <returns>
    ///   The release match attempts.
    /// </returns>
    IReadOnlyList<IReleaseMatchAttempt> GetReleaseMatchAttemptsForVideo(IVideo video);
}
