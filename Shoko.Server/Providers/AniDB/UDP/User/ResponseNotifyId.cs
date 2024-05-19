using Shoko.Server.Server;

namespace Shoko.Server.Providers.AniDB.UDP.User;

public class ResponseNotifyId
{
    /// <summary>
    /// Notify type
    /// </summary>
    public AniDBNotifyType Type { get; set; }

    /// <summary>
    /// Notification/message id
    /// </summary>
    public int ID { get; set; }
}
