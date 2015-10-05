using System;
using JMMModels.Childs;
using JMMServerModels.DB.Childs;

namespace JMMServerModels.DB
{
    public class CommandRequest
    {
        public string Id { get; set; }
        public string JMMUserId { get; set; }
        public CommandRequestPriority Priority { get; set; }
        public CommandRequestType CommandType { get; set; }
        public DateTime DateTimeUpdated { get; set; }
    }
}
