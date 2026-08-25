namespace Shoko.Server.Services.Mylist;

/// <summary>
/// What reconciling one MyList entry against the local watched state calls for.
/// </summary>
public enum MylistWatchedActionKind
{
    /// <summary>
    /// The two sides already agree, or the settings do not allow closing the
    /// difference in either direction.
    /// </summary>
    None = 0,

    /// <summary>
    /// Write AniDB's watched state onto the local record.
    /// </summary>
    Import = 1,

    /// <summary>
    /// Send the local watched state to AniDB.
    /// </summary>
    Export = 2,
}
