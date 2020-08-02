using System;

namespace Shoko.Server.API.SignalR.NLog
{
    public class LogEventArgs : EventArgs
    {
        public LogEvent LogEvent { get; set; }
        public string MethodName { get; set; }
        public string TargetName { get; set; }
    }
}