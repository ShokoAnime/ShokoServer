using System.Collections.Generic;
using System.ComponentModel;
using NLog;
using NLog.Config;
using NLog.Targets;

 namespace Shoko.Server.API.SignalR.NLog
{
    [Target("SignalR")]
    public class SignalRTarget : TargetWithLayout
    {
        [RequiredParameter]
        [DefaultValue("Log")]
        public string LogMethodName { get; set; }

        [RequiredParameter]
        [DefaultValue("GetBacklogs")]
        public string ConnectMethodName { get; set; }

        /// <summary>
        /// Gets or sets the max number of items to have in memory. If set to 0, then no backlog will be kept.
        /// </summary>
        /// <docgen category="Buffering Options" order="10" />
        [DefaultValue(10)]
        public int MaxLogsCount { get; set; }

        /// <summary>
        /// A list of the previous Log messages, up to <see cref="MaxLogsCount"/>. This is sent to the client on connection
        /// </summary>
        public IList<LogEvent> Logs { get; set; }

        public delegate void OnLog(LogEvent e);

        public event OnLog LogEventHandler;

        public SignalRTarget()
        {
            LogMethodName = "Log";
            ConnectMethodName = "GetBacklog";
            MaxLogsCount = 10;
            Logs = new List<LogEvent>(MaxLogsCount);
            OptimizeBufferReuse = true;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var renderedMessage = RenderLogEvent(Layout, logEvent);

            // Memory Target. Used for a bit of backlog to send on connection
            var item = new LogEvent(logEvent, renderedMessage);
            if (MaxLogsCount > 0)
            {
                if (Logs.Count >= MaxLogsCount)
                    Logs.RemoveAt(0);
                Logs.Add(item);
            }

            LogEventHandler?.Invoke(item);
        }
    }
}
