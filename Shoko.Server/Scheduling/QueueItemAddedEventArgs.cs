using System;
using System.Collections.Generic;

namespace Shoko.Server.Scheduling;

public class QueueItemAddedEventArgs : EventArgs
{
    public int WaitingJobsCount { get; set; }
    public int BlockedJobsCount { get; set; }
    public int TotalJobsCount { get; set; }
    public int ExecutingJobsCount { get; set; }
    public int ThreadCount { get; set; }
    public List<QueueItem> AddedItems { get; set; }
}
