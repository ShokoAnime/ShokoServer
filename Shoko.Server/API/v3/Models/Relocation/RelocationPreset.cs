using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Video.Relocation;

namespace Shoko.Server.API.v3.Models.Relocation;

/// <summary>
///   A relocation preset in the APIv3.
/// </summary>
/// <param name="preset">The preset info.</param>
/// <param name="provider">The provider info.</param>
public class RelocationPreset(RelocationPresetInfo preset, RelocationProviderInfo? provider)
{
    /// <summary>
    ///   The ID of the preset.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = preset.ID;

    /// <summary>
    ///   The provider ID for this preset.
    /// </summary>
    [Required]
    public Guid ProviderID { get; init; } = preset.ProviderID;

    /// <summary>
    ///   The friendly name of the preset, for display.
    /// </summary>
    [Required]
    public string Name { get; init; } = preset.Name;

    /// <summary>
    ///   Indicates if this preset is the default preset.
    /// </summary>
    [Required]
    public bool IsDefault { get; init; } = preset.IsDefault;

    /// <summary>
    /// Indicates that this preset is currently usable.
    /// </summary>
    [Required]
    public bool IsUsable { get; init; } = provider is not null;

    /// <summary>
    ///   Indicates that the preset has a configuration attached to it.
    /// </summary>
    [Required]
    public bool HasConfiguration { get; init; } = provider is null ? preset.Configuration is { Length: > 0 } : provider.ConfigurationInfo is not null;
}
