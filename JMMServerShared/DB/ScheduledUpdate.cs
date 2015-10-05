using System;
using JMMModels.Childs;

namespace JMMServerModels.DB
{
    public class ScheduledUpdate
    {
        public string Id { get; set; }
        public ScheduledUpdateType Type { get; set; }
        public DateTime LastUpdate { get; set; }
        public string Details { get; set; }
    }
}
