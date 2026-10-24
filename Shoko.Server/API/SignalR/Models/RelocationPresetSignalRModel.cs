using System;
using Shoko.Abstractions.Video.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a relocation preset is stored, updated or deleted.
/// </summary>
/// <param name="args">The event arguments.</param>
public class RelocationPresetSignalRModel(RelocationPresetEventArgs args)
{
    /// <summary>
    /// The preset ID.
    /// </summary>
    public Guid PresetID { get; } = args.RelocationPreset.ID;

    /// <summary>
    /// The preset's name.
    /// </summary>
    public string Name { get; } = args.RelocationPreset.Name;

    /// <summary>
    /// The ID of the relocation provider the preset configures.
    /// </summary>
    public Guid ProviderID { get; } = args.RelocationPreset.ProviderID;

    /// <summary>
    /// Whether the preset is the default one.
    /// </summary>
    public bool IsDefault { get; } = args.RelocationPreset.IsDefault;
}
