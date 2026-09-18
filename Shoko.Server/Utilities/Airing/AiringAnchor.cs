namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// What an airing schedule's learned offset is measured from.
/// </summary>
public enum AiringAnchor : byte
{
    /// <summary>
    /// Midnight UTC of the episode's AniDB air date. The fallback for every
    /// schedule, and the only anchor an Original schedule uses.
    /// </summary>
    AnidbDate = 0,

    /// <summary>
    /// The episode's earliest known real Original airing, anywhere. A simulpub
    /// follows the broadcast rather than its own channel's history.
    /// </summary>
    FirstOriginalAiring = 1,
}
