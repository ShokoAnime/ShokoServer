using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NLog;
using NLog.Targets;
using Shoko.Abstractions.Extensions;

namespace Shoko.Server.API.SignalR.NLog;

[Target("SignalR")]
public class SignalRTarget : TargetWithLayout
{
    [DefaultValue("Log")]
    public string LogMethodName { get; set; } = "Log";

    [DefaultValue("GetBacklog")]
    public string ConnectMethodName { get; set; } = "GetBacklog";

    /// <summary>
    /// Gets or sets the max number of items to have in memory. If set to 0, then no backlog will be kept.
    /// </summary>
    /// <docgen category="Buffering Options" order="10" />
    [DefaultValue(50)]
    public int MaxLogsCount { get; set; } = 50;

    /// <summary>
    /// A list of the previous Log messages, up to <see cref="MaxLogsCount"/>. This is sent to the client on connection
    /// </summary>
    public IList<LogEvent> Logs { get; set; }

    public delegate void OnLog(LogEvent e);

    public event OnLog LogEventHandler;

    public SignalRTarget()
    {
        Logs = new List<LogEvent>(MaxLogsCount);
    }

    protected override void Write(LogEventInfo logEvent)
    {
        var (threadId, processId, renderedMessage) = RenderLogEvent(Layout, logEvent).Split(',', 3);
        var item = new LogEvent(logEvent, renderedMessage, int.Parse(threadId), int.Parse(processId));
        if (MaxLogsCount > 0)
        {
            if (Logs.Count >= MaxLogsCount)
            {
                Logs.RemoveAt(0);
            }

            Logs.Add(item);
        }

        LogEventHandler?.Invoke(item);
    }
}
