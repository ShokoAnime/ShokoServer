using System;
using NLog;

namespace Shoko.Server.API.SignalR.NLog
{
    public class LogEvent : EventArgs
    {
        public string Level { get; set; }
        public DateTime TimeStamp { get; set; }
        public string LoggerName { get; set; }
        public string Caller { get; set; }
        public string Message { get; set; }

        public LogEvent()
        {
        }

        internal LogEvent(LogEventInfo eventInfo, string renderedMessage)
        {
            Level = eventInfo.Level.Name;
            TimeStamp = eventInfo.TimeStamp.ToUniversalTime();
            LoggerName = eventInfo.LoggerName;
            Caller = $"{eventInfo.CallerClassName}::{eventInfo.CallerMemberName}";
            Message = renderedMessage;
        }
    }
}