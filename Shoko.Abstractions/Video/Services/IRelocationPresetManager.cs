using System;
using System.Collections.Generic;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Relocation;

namespace Shoko.Abstractions.Video.Services;

/// <summary>
///   Service responsible for managing relocation presets.
/// </summary>
public interface IRelocationPresetManager
{
    #region Events

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
}
