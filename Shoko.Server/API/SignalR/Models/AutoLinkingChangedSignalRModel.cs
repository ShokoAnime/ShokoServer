using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when auto-linking to a source is turned off or on again for a
/// series.
/// </summary>
/// <param name="args">The event arguments.</param>
public class AutoLinkingChangedSignalRModel(AutoLinkingChangedEventArgs args)
{
    /// <summary>
    /// The Shoko series ID.
    /// </summary>
    public int ShokoSeriesID { get; } = args.Series.LocalID;

    /// <summary>
    /// The source auto-linking was turned off or on for.
    /// </summary>
    public MetadataSource Source { get; } = args.Source;

    /// <summary>
    /// Whether auto-linking to the source is now off.
    /// </summary>
    public bool IsDisabled { get; } = args.IsDisabled;
}
