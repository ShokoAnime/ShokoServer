using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Relocation;

namespace Shoko.Abstractions.Video.Services;

/// <summary>
///   Service responsible for relocating video files.
/// </summary>
public interface IVideoRelocationService
{
    #region Events

    /// <summary>
    ///   Event raised when the list of available providers has changed.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Event raised when a video file has been relocated.
    /// </summary>
    event EventHandler<VideoFileRelocatedEventArgs>? FileRelocated;

    #endregion

    #region Providers

    /// <summary>
    ///   Adds the needed parts for the service to function.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service, and will be
    ///   called during start-up. Calling it multiple times will have no effect.
    /// </remarks>
    /// <param name="parts">
    ///   The relocation providers.
    /// </param>
    void AddParts(IEnumerable<IRelocationProvider> parts);

    /// <summary>
    ///   Gets all providers that are available.
    /// </summary>
    /// <returns>
    ///   The available provider infos.
    /// </returns>
    IEnumerable<RelocationProviderInfo> GetAvailableProviders();

    /// <summary>
    ///   Gets the <see cref="RelocationProviderInfo"/> for a given plugin.
    /// </summary>
    /// <param name="plugin">
    ///   The plugin.
    /// </param>
    /// <returns>
    ///   The provider info.
    /// </returns>
    IReadOnlyList<RelocationProviderInfo> GetProviderInfo(IPlugin plugin);

    /// <summary>
    ///   Gets the provider info for the given provider ID.
    /// </summary>
    /// <param name="providerID">
    ///   The provider ID.
    /// </param>
    /// <returns>
    ///   The <see cref="RelocationProviderInfo"/> if available in the current runtime environment, otherwise <c>null</c>.
    /// </returns>
    RelocationProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Gets the <see cref="RelocationProviderInfo"/> for the provider.
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
    RelocationProviderInfo GetProviderInfo(IRelocationProvider provider);

    /// <summary>
    ///   Gets the <see cref="RelocationProviderInfo"/> for the specified type.
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
    RelocationProviderInfo GetProviderInfo<TProvider>() where TProvider : IRelocationProvider;

    #endregion

    #region Relocation Methods

    /// <summary>
    ///   Schedules a job to relocate all files for a video using the default
    ///   relocation preset.
    /// </summary>
    /// <param name="video">
    ///   The video to schedule the files for potential relocation.
    /// </param>
    /// <param name="prioritize">
    ///   If set to <c>true</c>, then the job will be given higher than
    ///   default priority in the queue.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of
    ///   scheduling the job in the queue.
    /// </returns>
    Task ScheduleAutoRelocationForVideo(IVideo video, bool prioritize = false);

    /// <summary>
    ///   Schedules a job to relocate the video file using the default
    ///   relocation preset.
    /// </summary>
    /// <param name="file">
    ///   The video file to schedule for potential relocation.
    /// </param>
    /// <param name="prioritize">
    ///   If set to <c>true</c>, then the job will be given higher than
    ///   default priority in the queue.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of
    ///   scheduling the job in the queue.
    /// </returns>
    Task ScheduleAutoRelocationForVideoFile(IVideoFile file, bool prioritize = false);

    /// <summary>
    ///   Chains a relocation job for all files for a video immediately after the
    ///   currently-executing job. Intended for use inside a running job.
    /// </summary>
    /// <param name="video">
    ///   The video whose files should be scheduled for potential relocation.
    /// </param>
    /// <param name="cancellationToken">
    ///   Token used to cancel the chain registration.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of
    ///   registering the chained job.
    /// </returns>
    Task ChainAutoRelocationForVideo(IVideo video, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Chains a relocation job for a video file immediately after the
    ///   currently-executing job. Intended for use inside a running job.
    /// </summary>
    /// <param name="file">
    ///   The video file to chain for potential relocation.
    /// </param>
    /// <param name="cancellationToken">
    ///   Token used to cancel the chain registration.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of
    ///   registering the chained job.
    /// </returns>
    Task ChainAutoRelocationForVideoFile(IVideoFile file, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Potentially relocate a video file using the provided auto-relocate
    ///   request.
    /// </summary>
    /// <remarks>
    ///   If the video file is already being relocated then the current
    ///   operation will be queued for when it finishes, unless
    ///   <seealso cref="AutoRelocateRequest.CancelIfRunning"/> is set to
    ///   <c>true</c>.
    /// </remarks>
    /// <param name="file">
    ///   The video file to potentially relocate.
    /// </param>
    /// <param name="request">
    ///   The auto-relocate request. Will use a new request with the default
    ///   values if not provided or if <c>null</c>.
    /// </param>
    /// <returns>
    ///   The relocation result.
    /// </returns>
    Task<RelocationResponse> AutoRelocateFile(IVideoFile file, AutoRelocateRequest? request = null);

    /// <summary>
    ///   Potentially relocate a video file using the provided directly-relocate
    ///   request.
    /// </summary>
    /// <remarks>
    ///   If the video file is already being relocated then the current
    ///   operation will be queued for when it finishes, unless
    ///   <seealso cref="DirectlyRelocateRequest.CancelIfRunning"/> is set to
    ///   <c>true</c>.
    /// </remarks>
    /// <param name="file">
    ///   The video file to potentially relocate.
    /// </param>
    /// <param name="request">
    ///   The directly-relocate request.
    /// </param>
    /// <returns>
    ///   The relocation result.
    /// </returns>
    Task<RelocationResponse> DirectlyRelocateFile(IVideoFile file, DirectlyRelocateRequest request);

    #endregion

    #region Utility Methods

    /// <summary>
    ///   Get the first managed folder marked as a destination with enough space
    ///   for the video file, if any.
    /// </summary>
    /// <param name="context">
    ///   The relocation context.
    /// </param>
    /// <returns>
    ///   The first managed folder marked as a destination with enough space for
    ///   the video file, if any.
    /// </returns>
    IManagedFolder? GetFirstDestinationWithSpace(RelocationContext context);

    /// <summary>
    ///   Check if the managed folder has enough space for the video file.
    /// </summary>
    /// <param name="folder">
    ///   The managed folder to check.
    /// </param>
    /// <param name="file">
    ///   The video file to check.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the managed folder has enough space for the video file,
    ///   <c>false</c> otherwise.
    /// </returns>
    bool ManagedFolderHasSpace(IManagedFolder folder, IVideoFile file);

    /// <summary>
    ///   Get the location of the folder that contains a file for the latest (airdate) episode in the current collection.
    /// </summary>
    /// <remarks>
    ///   Will only look for files in managed folders of type <see cref="DropFolderType.Excluded"/> or <see cref="DropFolderType.Destination"/>.
    /// </remarks>
    /// <param name="context">
    ///   The relocation context.
    /// </param>
    /// <returns>
    ///   A tuple containing the managed folder and relative path, or
    ///   <c>null</c> if existing series location could not be found.
    /// </returns>
    public (IManagedFolder ManagedFolder, string RelativePath)? GetExistingSeriesLocationWithSpace(RelocationContext context);

    #endregion
}
