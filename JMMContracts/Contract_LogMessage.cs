using System;

namespace JMMContracts
{
    public class Contract_LogMessage
    {
        public int LogMessageID { get; set; }
        public string LogType { get; set; }
        public string LogContent { get; set; }
        public DateTime LogDate { get; set; }
    }
}