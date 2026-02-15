using System;
using Shoko.Server.Server;

# nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_NotifyQueue
{
    public int AniDB_NotifyQueueID { get; set; }

    public AniDBNotifyType Type { get; set; }

    public int ID { get; set; }

    public DateTime AddedAt { get; set; }
}
