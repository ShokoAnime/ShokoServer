using System;

namespace Shoko.Server.Providers.AniDB.UDP.User;

public class ResponseNotificationContent
{
    /// <summary>
    /// ID of the related type. Currently only anime is supported.
    /// </summary>
    public int RelatedTypeID { get; set; }

    /// <summary>
    /// Type of notification
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Number of pending events
    /// </summary>
    public int PendingEvents { get; set; }

    /// <summary>
    /// Time at which the notification has been sent
    /// </summary>
    public DateTime SentTime { get; set; }

    /// <summary>
    /// Name of the related type. Currently only anime is supported.
    /// </summary>
    public string RelatedTypeName { get; set; }

    /// <summary>
    /// IDs of all affected files
    /// </summary>
    public int[] FileIDs { get; set; }
}
