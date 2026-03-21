using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class AvdumpEventSignalRModel
{
    [JsonConverter(typeof(StringEnumConverter))]
    public AnidbAvdumpEventType Type { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? SessionID { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<int>? VideoIDs { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public double? Progress { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? SucceededCreqCount { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? FailedCreqCount { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? PendingCreqCount { get; set; }

    [JsonProperty("ed2ks", NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<string>? ED2Ks { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ErrorMessage { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ExceptionStackTrace { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? StartedAt { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? EndedAt { get; set; }

    public AvdumpEventSignalRModel(AnidbAvdumpEventArgs eventArgs)
    {
        SessionID = eventArgs.SessionID;
        VideoIDs = eventArgs.VideoIDs;
        Type = eventArgs.Type;
        Progress = eventArgs.Progress;
        SucceededCreqCount = eventArgs.SucceededCreqCount;
        FailedCreqCount = eventArgs.FailedCreqCount;
        PendingCreqCount = eventArgs.PendingCreqCount;
        ED2Ks = eventArgs.ED2Ks;
        Message = eventArgs.Message;
        ErrorMessage = eventArgs.ErrorMessage;
        ExceptionStackTrace = eventArgs.Exception?.StackTrace;
        StartedAt = eventArgs.StartedAt?.ToUniversalTime();
        EndedAt = eventArgs.EndedAt?.ToUniversalTime();
    }

    public AvdumpEventSignalRModel(AVDumpHelper.AVDumpSession session)
    {
        SessionID = session.SessionID;
        VideoIDs = session.VideoIDs;
        Type = AnidbAvdumpEventType.Restore;
        Progress = session.Progress;
        SucceededCreqCount = session.SucceededCreqCount;
        FailedCreqCount = session.FailedCreqCount;
        PendingCreqCount = session.PendingCreqCount;
        ED2Ks = session.ED2Ks.ToList();
        StartedAt = session.StartedAt.ToUniversalTime();
    }
}
