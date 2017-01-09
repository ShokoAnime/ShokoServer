using System;

namespace Shoko.Models.Server
{
    public class CommandRequest
    {
        public int CommandRequestID { get; private set; }
        public int Priority { get; set; }
        public int CommandType { get; set; }
        public string CommandID { get; set; }
        public string CommandDetails { get; set; }
        public DateTime DateTimeUpdated { get; set; }
    }
}