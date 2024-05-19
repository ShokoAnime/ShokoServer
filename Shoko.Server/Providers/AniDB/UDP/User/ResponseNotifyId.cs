namespace Shoko.Server.Providers.AniDB.UDP.User;

public class ResponseNotifyId
{
    /// <summary>
    /// Is notification a message
    /// </summary>
    public bool Message { get; set; }

    /// <summary>
    /// Is notification an actual notification
    /// </summary>
    public bool Notification => !Message;

    /// <summary>
    /// Notification/message id
    /// </summary>
    public int ID { get; set; }
}
