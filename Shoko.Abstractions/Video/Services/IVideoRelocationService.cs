using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Relocation;

namespace Shoko.Abstractions.Video.Services;

/// <summary>
///   Service responsible for managing relocation presets and relocating video
///   files.
/// </summary>
public interface IVideoRelocationService
{
    #region Events

    /// <summary>
    ///   Event raised when the list of available providers has changed.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Event raised when a new relocation preset has been stored in the database.
    /// </summary>
    event EventHandler<RelocationPresetEventArgs>? PresetStored;

    /// <summary>
    ///   Event raised when an existing relocation preset has been updated in the database.
    /// </summary>
    event EventHandler<RelocationPresetEventArgs>? PresetUpdated;

    /// <summary>
    ///   Event raised when an existing relocation preset has been deleted from the database.
    /// </summary>
    event EventHandler<RelocationPresetEventArgs>? PresetDeleted;

    /// <summary>
    ///   Event raised when a video file has been relocated.
    /// </summary>
    event EventHandler<VideoFileRelocatedEventArgs>? FileRelocated;

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

    #region Presets

    /// <summary>
    ///   Gets the default preset.
    /// </summary>
    /// <returns>
    ///   The <see cref="RelocationPresetInfo"/> for the default preset, or
    ///   <c>null</c> if currently not set.
    /// </returns>
    RelocationPresetInfo? GetDefaultPreset();

    /// <summary>
    ///   Gets all stored presets, optionally filtered by availability.
    /// </summary>
    /// <param name="available">
    ///   If <c>true</c>, only returns available presets.
    ///   If <c>false</c>, only returns unavailable presets.
    /// </param>
    /// <returns>
    ///   The stored presets.
    /// </returns>
    IEnumerable<RelocationPresetInfo> GetStoredPresets(bool? available = null);

    /// <summary>
    ///   Gets the <see cref="RelocationPresetInfo"/> for a given provider by ID.
    /// </summary>
    /// <param name="providerID">
    ///   The provider ID.
    /// </param>
    /// <returns>
    ///   The stored presets.
    /// </returns>
    IReadOnlyList<RelocationPresetInfo> GetStoredPresets(Guid providerID);

    /// <summary>
    ///   Gets the <see cref="RelocationPresetInfo"/> for a given provider by ID.
    /// </summary>
    /// <param name="provider">
    ///   The provider.
    /// </param>
    /// <returns>
    ///   The stored presets.
    /// </returns>
    IReadOnlyList<RelocationPresetInfo> GetStoredPresets(IRelocationProvider provider);

    /// <summary>
    ///   Gets the <see cref="RelocationPresetInfo"/> for a given plugin.
    /// </summary>
    /// <param name="plugin">
    ///   The plugin.
    /// </param>
    /// <returns>
    ///   The stored presets.
    /// </returns>
    IReadOnlyList<RelocationPresetInfo> GetStoredPresets(IPlugin plugin);

    /// <summary>
    ///   Gets the <see cref="RelocationPresetInfo"/> for a given preset ID.
    /// </summary>
    /// <param name="presetID">
    ///   The preset ID.
    /// </param>
    /// <returns>
    ///   The <see cref="RelocationPresetInfo"/> for the stored preset, if it's
    ///   available in the database, otherwise <c>null</c>.
    /// </returns>
    RelocationPresetInfo? GetStoredPreset(Guid presetID);

    /// <summary>
    ///   Gets the <see cref="RelocationPresetInfo"/> for a given preset name.
    /// </summary>
    /// <param name="name">
    ///   The preset name.
    /// </param>
    /// <returns>
    ///   The <see cref="RelocationPresetInfo"/> for the stored preset, if it's
    ///   available in the database, otherwise <c>null</c>.
    /// </returns>
    RelocationPresetInfo? GetStoredPreset(string? name);

    /// <summary>
    ///   Store a new preset.
    /// </summary>
    /// <param name="provider">
    ///   The provider to store a new preset for.
    /// </param>
    /// <param name="name">
    ///   The friendly name of the preset. Must be non-empty.
    /// </param>
    /// <param name="configuration">
    ///   The configuration to store.
    /// </param>
    /// <param name="setDefault">
    ///   Set the new preset as the default preset.
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
    ///   The <see cref="RelocationPresetInfo"/> for the newly stored preset.
    /// </returns>
    RelocationPresetInfo StorePreset(IRelocationProvider provider, string name, IRelocationProviderConfiguration? configuration = null, bool setDefault = false);

    /// <summary>
    ///   Store a new preset.
    /// </summary>
    /// <param name="provider">
    ///   The provider to store a new preset for.
    /// </param>
    /// <param name="name">
    ///   The friendly name of the preset. Must be non-empty.
    /// </param>
    /// <param name="configuration">
    ///   The configuration to store.
    /// </param>
    /// <param name="setDefault">
    ///   Set the new preset as the default preset.
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
    ///   The <see cref="RelocationPresetInfo"/> for the newly stored preset.
    /// </returns>
    RelocationPresetInfo StorePreset<TConfig>(IRelocationProvider<TConfig> provider, string name, TConfig configuration, bool setDefault = false) where TConfig : IRelocationProviderConfiguration;

    /// <summary>
    ///   Updates the saved preset with the new details.
    /// </summary>
    /// <param name="preset">
    ///   The preset to update.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="preset"/> is not stored in the database.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="preset"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The configuration is invalid for the provider.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the preset was updated, <c>false</c> otherwise.
    /// </returns>
    bool UpdatePreset(IStoredRelocationPreset preset);

    /// <summary>
    ///   Deletes the given preset.
    /// </summary>
    /// <param name="preset">
    ///   The preset to delete.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the preset was deleted, <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when attempting to delete the currently in-use default preset or
    ///   the preset is not stored in the database.
    /// </exception>
    void DeletePreset(IStoredRelocationPreset preset);

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
