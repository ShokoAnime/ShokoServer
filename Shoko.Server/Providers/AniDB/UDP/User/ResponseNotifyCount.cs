namespace Shoko.Server.Providers.AniDB.UDP.User;

public class ResponseNotificationCount
{
    /// <summary>
    /// Number of pending file notifications
    /// </summary>
    public int Files { get; set; }

    /// <summary>
    /// Number of unread messages
    /// </summary>
    public int Messages { get; set; }

    /// <summary>
    /// Number of online buddies
    /// </summary>
    public int? BuddiesOnline { get; set; }
}
