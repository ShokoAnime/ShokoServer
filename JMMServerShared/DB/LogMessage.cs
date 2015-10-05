using System;

namespace JMMServerModels.DB
{
    public class LogMessage
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Content { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
