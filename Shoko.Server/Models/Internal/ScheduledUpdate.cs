using System;

namespace Shoko.Server.Models.Internal;

public class ScheduledUpdate
{
    public int ScheduledUpdateID { get; set; }

    public int UpdateType { get; set; }

    public DateTime LastUpdate { get; set; }

    public string UpdateDetails { get; set; }
}
