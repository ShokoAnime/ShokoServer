using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

 namespace Shoko.Server.API.SignalR.NLog
{
    [Target("SignalR")]
    public class SignalRTarget : TargetWithLayout
    {
        [RequiredParameter]
        [DefaultValue("Log")]
        public Layout MethodName { get; set; }

        public delegate Task OnLog(LogEventArgs e);

        public event OnLog LogEventHandler;

        public SignalRTarget()
        {
            MethodName = "Log";
            OptimizeBufferReuse = true;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var renderedMessage = Layout.Render(logEvent);
            var methodName = MethodName.Render(logEvent);
            var item = new LogEvent(logEvent, renderedMessage);
            LogEventHandler?.Invoke(new LogEventArgs {LogEvent = item, MethodName = methodName, TargetName = Name});
        }
    }
}
