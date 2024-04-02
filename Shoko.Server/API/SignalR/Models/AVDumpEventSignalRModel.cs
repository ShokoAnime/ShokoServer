using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class AVDumpEventSignalRModel
{
    [JsonConverter(typeof(StringEnumConverter))]
    public AVDumpEventType Type { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? SessionID { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<int>? VideoIDs { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? CommandID { get; set; }

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

    public AVDumpEventSignalRModel(AVDumpEventArgs eventArgs)
    {
        SessionID = eventArgs.SessionID;
        VideoIDs = eventArgs.VideoIDs;
        CommandID = eventArgs.CommandID;
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

    public AVDumpEventSignalRModel(AVDumpHelper.AVDumpSession session)
    {
        SessionID = session.SessionID;
        VideoIDs = session.VideoIDs;
        CommandID = session.CommandID;
        Type = AVDumpEventType.Restore;
        Progress = session.Progress;
        SucceededCreqCount = session.SucceededCreqCount;
        FailedCreqCount = session.FailedCreqCount;
        PendingCreqCount = session.PendingCreqCount;
        ED2Ks = session.ED2Ks.ToList();
        StartedAt = session.StartedAt.ToUniversalTime();
    }
}
