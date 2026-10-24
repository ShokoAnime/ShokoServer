using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when an episode is hidden or shown again.
/// </summary>
/// <param name="args">The event arguments.</param>
public class EpisodeHiddenChangedSignalRModel(EpisodeHiddenChangedEventArgs args)
{
    /// <summary>
    /// The episode's full ID.
    /// </summary>
    public string EpisodeID { get; } = args.EpisodeID.ToString();

    /// <summary>
    /// The Shoko episode ID, for a Shoko episode.
    /// </summary>
    public int? ShokoEpisodeID { get; } = OrderingChangedSignalRModel.ShokoID(args.EpisodeID);

    /// <summary>
    /// Whether the episode is now hidden.
    /// </summary>
    public bool IsHidden { get; } = args.IsHidden;
}
