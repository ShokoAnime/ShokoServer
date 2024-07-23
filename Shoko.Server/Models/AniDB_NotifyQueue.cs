using System;
using Shoko.Server.Server;

namespace Shoko.Server.Models;

public class AniDB_NotifyQueue
{
    public int AniDB_NotifyQueueID { get; set; }
    public AniDBNotifyType Type { get; set; }
    public int ID { get; set; }
    public DateTime AddedAt { get; set; }
}
