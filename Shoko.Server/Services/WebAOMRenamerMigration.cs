using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using MessagePack;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Plugin;

namespace Shoko.Server.Services;

/// <summary>
///   What the core still knows of the WebAOM renamer, which moved to its own
///   bundled plugin: the provider's IDs before and after the move, and how a
///   preset's settings from an older version are carried over.
/// </summary>
internal static class WebAOMRenamerMigration
{
    #region IDs

    /// <summary>
    ///   The ID of the bundled WebAOM plugin.
    /// </summary>
    public static readonly Guid PluginID = new("8c8d75f2-cd0c-5c0b-9a9e-8c2c6d3e5f1a");

    /// <summary>
    ///   The full name of the renamer's type in the plugin.
    /// </summary>
    public const string ProviderTypeName = "Shoko.Plugin.WebAOM.WebAOMRenamer";

    /// <summary>
    ///   The full name the renamer's type had while the core shipped it.
    /// </summary>
    public const string LegacyProviderTypeName = "Shoko.Server.Renamer.WebAOMRenamer";

    /// <summary>
    ///   The renamer's ID in the plugin.
    /// </summary>
    public static readonly Guid ProviderID = VideoRelocationService.GetProviderID(ProviderTypeName, PluginID);

    /// <summary>
    ///   The ID the renamer had while the core shipped it, which presets saved
    ///   before the move still point to.
    /// </summary>
    public static readonly Guid LegacyProviderID = VideoRelocationService.GetProviderID(LegacyProviderTypeName, CorePlugin.StaticID);

    #endregion

    #region Presets

    /// <summary>
    ///   Points the presets of the renamer the core shipped to the renamer in
    ///   the plugin, keeping their settings as they are.
    /// </summary>
    /// <param name="presets">The stored presets.</param>
    /// <returns>The presets that were changed, to be saved.</returns>
    public static IReadOnlyList<StoredRelocationPreset> RepointPresets(IEnumerable<StoredRelocationPreset> presets)
    {
        var changed = presets.Where(preset => preset.ProviderID == LegacyProviderID).ToList();
        foreach (var preset in changed)
            preset.ProviderID = ProviderID;

        return changed;
    }

    #endregion

    #region Settings

    /// <summary>
    ///   How the renamer settings of an older version were packed.
    /// </summary>
    private static readonly MessagePackSerializerOptions _packedOptions = MessagePackSerializer.DefaultOptions.WithCompression(MessagePackCompression.Lz4BlockArray);

    /// <summary>
    ///   Makes a provider's settings from the settings an older version packed
    ///   with their type, which may no longer exist, by reading them by name.
    /// </summary>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="provider">The provider the settings are for.</param>
    /// <param name="packed">The packed settings, or <c>null</c> for the defaults.</param>
    /// <returns>The settings, or <c>null</c> when the provider has none.</returns>
    public static IRelocationProviderConfiguration? CarryOverPackedSettings(IConfigurationService configurationService, RelocationProviderInfo provider, byte[]? packed)
        => CarryOverSettings(configurationService, provider, packed is { Length: > 0 } ? MessagePackSerializer.ConvertToJson(packed, _packedOptions) : null);

    /// <summary>
    ///   Makes a provider's settings from the values an older version stored,
    ///   taking every value whose name matches one of the settings and leaving
    ///   the defaults for the rest.
    /// </summary>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="provider">The provider the settings are for.</param>
    /// <param name="legacyJson">The stored values as a JSON object, or <c>null</c> for the defaults.</param>
    /// <returns>The settings, or <c>null</c> when the provider has none.</returns>
    public static IRelocationProviderConfiguration? CarryOverSettings(IConfigurationService configurationService, RelocationProviderInfo provider, string? legacyJson)
    {
        if (provider.ConfigurationInfo is not { } info)
            return null;

        var defaults = configurationService.New(info);
        if (string.IsNullOrWhiteSpace(legacyJson) || JsonNode.Parse(legacyJson) is not JsonObject legacy)
            return (IRelocationProviderConfiguration)defaults;

        var current = JsonNode.Parse(configurationService.Serialize(defaults))!.AsObject();
        foreach (var (key, value) in legacy)
        {
            var name = current.Select(property => property.Key).FirstOrDefault(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));
            if (name is not null)
                current[name] = value?.DeepClone();
        }

        return (IRelocationProviderConfiguration)configurationService.Deserialize(info, current.ToJsonString());
    }

    #endregion
}
