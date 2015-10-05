using System;

namespace JMMModels.Childs
{
    public class UserStats
    {
        public string JMMUserId { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }


    }
}
