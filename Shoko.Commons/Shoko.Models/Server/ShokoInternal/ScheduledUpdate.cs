using System;

namespace Shoko.Models.Server
{
    public class ScheduledUpdate
    {
        public int ScheduledUpdateID { get; set; }
        public int UpdateType { get; set; }
        public DateTime LastUpdate { get; set; }
        public string UpdateDetails { get; set; }
    }
}