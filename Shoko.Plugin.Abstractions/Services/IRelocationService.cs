using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Relocation;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
///   Service responsible for managing relocation pipes and relocating video
///   files.
/// </summary>
public interface IRelocationService
{
    #region Events

    /// <summary>
    ///   Event raised when the list of available providers has changed.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Event raised when a new relocation pipe has been stored in the database.
    /// </summary>
    event EventHandler<RelocationPipeEventArgs>? PipeStored;

    /// <summary>
    ///   Event raised when an existing relocation pipe has been updated in the database.
    /// </summary>
    event EventHandler<RelocationPipeEventArgs>? PipeUpdated;

    /// <summary>
    ///   Event raised when an existing relocation pipe has been deleted from the database.
    /// </summary>
    event EventHandler<RelocationPipeEventArgs>? PipeDeleted;

    /// <summary>
    ///   Event raised when a video file has been relocated.
    /// </summary>
    event EventHandler<FileRelocatedEventArgs>? FileRelocated;

    #endregion

    #region Settings

    /// <summary>
    /// Indicates that we should rename a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    bool RenameOnImport { get; set; }

    /// <summary>
    /// Indicates that we should move a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    bool MoveOnImport { get; set; }

    /// <summary>
    /// Indicates that we should relocate a video file that lives inside a
    /// drop destination managed folder that's not also a drop source on import.
    /// </summary>
    bool AllowRelocationInsideDestinationOnImport { get; set; }

    #endregion

    #region Providers

    /// <summary>
    ///   Adds the needed parts for the service to function.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service, and will be
    ///   called during start-up. Calling it multiple times will have no effect.
    /// </remarks>
    /// <param name="pipes">
    ///   The hash pipes.
    /// </param>
    void AddParts(IEnumerable<IRelocationProvider> pipes);

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

    #region Pipes

    /// <summary>
    ///   Gets the default pipe.
    /// </summary>
    /// <returns>
    ///   The <see cref="RelocationPipeInfo"/> for the default pipe, or
    ///   <c>null</c> if currently not set.
    /// </returns>
    RelocationPipeInfo? GetDefaultPipe();

    /// <summary>
    ///   Gets all stored pipes, optionally filtered by availability.
    /// </summary>
    /// <param name="available">
    ///   If <c>true</c>, only returns available pipes.
    ///   If <c>false</c>, only returns unavailable pipes.
    /// </param>
    /// <returns>
    ///   The stored pipes.
    /// </returns>
    IEnumerable<RelocationPipeInfo> GetStoredPipes(bool? available = null);

    /// <summary>
    ///   Gets the <see cref="RelocationPipeInfo"/> for a given provider by ID.
    /// </summary>
    /// <param name="providerID">
    ///   The provider ID.
    /// </param>
    /// <returns>
    ///   The stored pipes.
    /// </returns>
    IReadOnlyList<RelocationPipeInfo> GetStoredPipes(Guid providerID);

    /// <summary>
    ///   Gets the <see cref="RelocationPipeInfo"/> for a given provider by ID.
    /// </summary>
    /// <param name="provider">
    ///   The provider.
    /// </param>
    /// <returns>
    ///   The stored pipes.
    /// </returns>
    IReadOnlyList<RelocationPipeInfo> GetStoredPipes(IRelocationProvider provider);

    /// <summary>
    ///   Gets the <see cref="RelocationPipeInfo"/> for a given plugin.
    /// </summary>
    /// <param name="plugin">
    ///   The plugin.
    /// </param>
    /// <returns>
    ///   The stored pipes.
    /// </returns>
    IReadOnlyList<RelocationPipeInfo> GetStoredPipes(IPlugin plugin);

    /// <summary>
    ///   Gets the <see cref="RelocationPipeInfo"/> for a given pipe ID.
    /// </summary>
    /// <param name="pipeID">
    ///   The pipe ID.
    /// </param>
    /// <returns>
    ///   The <see cref="RelocationPipeInfo"/> for the stored pipe, if it's
    ///   available in the database, otherwise <c>null</c>.
    /// </returns>
    RelocationPipeInfo? GetStoredPipe(Guid pipeID);

    /// <summary>
    ///   Gets the <see cref="RelocationPipeInfo"/> for a given pipe name.
    /// </summary>
    /// <param name="name">
    ///   The pipe name.
    /// </param>
    /// <returns>
    ///   The <see cref="RelocationPipeInfo"/> for the stored pipe, if it's
    ///   available in the database, otherwise <c>null</c>.
    /// </returns>
    RelocationPipeInfo? GetStoredPipe(string? name);

    /// <summary>
    ///   Store a new pipe.
    /// </summary>
    /// <param name="provider">
    ///   The provider to store a new pipe for.
    /// </param>
    /// <param name="name">
    ///   The friendly name of the pipe. Must be non-empty.
    /// </param>
    /// <param name="configuration">
    ///   The configuration to store.
    /// </param>
    /// <param name="setDefault">
    ///   Set the new pipe as the default pipe.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="name"/> is <c>null</c> or empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The configuration is invalid for the provider.
    /// </exception>
    /// <returns>
    ///   The <see cref="RelocationPipeInfo"/> for the newly stored pipe.
    /// </returns>
    RelocationPipeInfo StorePipe(IRelocationProvider provider, string name, IRelocationProviderConfiguration? configuration = null, bool setDefault = false);

    /// <summary>
    ///   Store a new pipe.
    /// </summary>
    /// <param name="provider">
    ///   The provider to store a new pipe for.
    /// </param>
    /// <param name="name">
    ///   The friendly name of the pipe. Must be non-empty.
    /// </param>
    /// <param name="configuration">
    ///   The configuration to store.
    /// </param>
    /// <param name="setDefault">
    ///   Set the new pipe as the default pipe.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="name"/> is <c>null</c> or empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The configuration is invalid for the provider.
    /// </exception>
    /// <returns>
    ///   The <see cref="RelocationPipeInfo"/> for the newly stored pipe.
    /// </returns>
    RelocationPipeInfo StorePipe<TConfig>(IRelocationProvider<TConfig> provider, string name, TConfig configuration, bool setDefault = false) where TConfig : IRelocationProviderConfiguration;

    /// <summary>
    ///   Updates the saved pipe with the new details.
    /// </summary>
    /// <param name="pipe">
    ///   The pipe to update.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="pipe"/> is not stored in the database.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="pipe"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The configuration is invalid for the provider.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the pipe was updated, <c>false</c> otherwise.
    /// </returns>
    bool UpdatePipe(IStoredRelocationPipe pipe);

    /// <summary>
    ///   Deletes the given pipe.
    /// </summary>
    /// <param name="pipe">
    ///   The pipe to delete.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the pipe was deleted, <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when attempting to delete the currently in-use default pipe or
    ///   the pipe is not stored in the database.
    /// </exception>
    void DeletePipe(IStoredRelocationPipe pipe);

    #endregion

    #region Relocation Methods

    /// <summary>
    ///   Schedules a job to relocate all files for a video using the default
    ///   relocation pipe.
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
    ///   relocation pipe.
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
