using System;
using Shoko.Server.Providers.AniDB;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class AniDBStatusUpdateSignalRModel
{
    /// <summary>
    /// The value of the UpdateType, is the ban active, is it waiting on a response, etc
    /// </summary>
    public bool Value { get; set; }

    /// <summary>
    /// Auxiliary Message for some states
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Update type, Ban, Invalid Session, Waiting on Response, etc
    /// </summary>
    public UpdateType UpdateType { get; set; }

    /// <summary>
    /// When was it updated, usually Now, but may not be
    /// </summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>
    /// If we are pausing the queue, then for how long(er)
    /// </summary>
    public int PauseTimeSecs { get; set; }

    public AniDBStatusUpdateSignalRModel(AniDBStateUpdate update)
    {
        Value = update.Value;
        Message = update.Message;
        UpdateType = update.UpdateType;
        UpdateTime = update.UpdateTime.ToUniversalTime();
        PauseTimeSecs = update.PauseTimeSecs;
    }
}
