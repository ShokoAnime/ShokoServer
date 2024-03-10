namespace Shoko.Server.Scheduling;

public class QueueStateContext
{
    public int WaitingTriggersCount { get; set; }
    public int BlockedTriggersCount { get; set; }
    public int TotalTriggersCount { get; set; }
    public int ThreadCount { get; set; }
    public QueueItem[] CurrentlyExecuting { get; set; }
    public QueueItem[] Waiting { get; set; }
}
