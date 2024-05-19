using System;

namespace Shoko.Server.Providers.AniDB.UDP.User;

public class ResponseMessageContent
{
    /// <summary>
    /// Message ID
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// Sender's ID
    /// </summary>
    public int SenderID { get; set; }

    /// <summary>
    /// Sender's name
    /// </summary>
    public string SenderName { get; set; }

    /// <summary>
    /// Time at which the message has been sent
    /// </summary>
    public DateTime SentTime { get; set; }

    /// <summary>
    /// Type of message
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Message title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Message body/content
    /// </summary>
    public string Body { get; set; }
}
