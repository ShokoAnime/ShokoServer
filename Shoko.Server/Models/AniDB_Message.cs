using System;

namespace Shoko.Server.Models;

public class AniDB_Message
{
    public int AniDB_MessageID { get; set; }
    public int MessageID { get; set; }
    public int FromUserId { get; set; }
    public string FromUserName { get; set; }
    public DateTime Date { get; set; }
    public int Type { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
}
