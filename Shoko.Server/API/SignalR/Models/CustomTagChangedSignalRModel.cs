using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a custom tag is added, updated or removed.
/// </summary>
/// <param name="args">The event arguments.</param>
public class CustomTagChangedSignalRModel(CustomTagChangedEventArgs args)
{
    /// <summary>
    /// The custom tag ID.
    /// </summary>
    public int TagID { get; } = args.Tag.LocalID;

    /// <summary>
    /// The tag's name.
    /// </summary>
    public string Name { get; } = args.Tag.Name;

    /// <summary>
    /// The name before an update renamed it, or <c>null</c>.
    /// </summary>
    public string? PreviousName { get; } = args.PreviousName;
}
