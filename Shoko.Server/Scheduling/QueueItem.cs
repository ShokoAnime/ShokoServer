using System;

namespace Shoko.Server.Scheduling;

public struct QueueItem
{
    public string Key { get; init; }
    public string JobType { get; init; }
    public string Description { get; init; }
    public DateTime? StartTime { get; init; }
    public bool Running { get; init; }
    public bool Blocked { get; init; }
}
