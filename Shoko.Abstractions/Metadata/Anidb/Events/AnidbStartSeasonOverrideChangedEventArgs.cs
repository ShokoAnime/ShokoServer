using System;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Anidb.Events;

/// <summary>
///   Dispatched when the start season override of an AniDB anime is set,
///   changed or removed.
/// </summary>
public class AnidbStartSeasonOverrideChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The AniDB anime ID.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The override before the change, or <c>null</c> when it was just set.
    /// </summary>
    public required AnidbStartSeasonOverride? Previous { get; init; }

    /// <summary>
    ///   The override after the change, or <c>null</c> when it was removed.
    /// </summary>
    public required AnidbStartSeasonOverride? Current { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
