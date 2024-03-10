using System;
using System.Collections.Generic;

namespace Shoko.Server.Scheduling;

public class QueueChangedEventArgs : EventArgs
{
    public int WaitingJobsCount { get; set; }
    public int BlockedJobsCount { get; set; }
    public int TotalJobsCount { get; set; }
    public int ThreadCount { get; set; }
    public List<QueueItem> AddedItems { get; set; }
    public List<QueueItem> RemovedItems { get; set; }
    public List<QueueItem> ExecutingItems { get; set; }
    public List<QueueItem> WaitingItems { get; set; }
}
