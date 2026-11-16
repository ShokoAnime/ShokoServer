using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Core;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
///   One thing the start said, as the startup hub sends it.
/// </summary>
public class StartupMessageSignalRModel
{
    /// <summary>
    ///   What the start was doing.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///   When it said so, in UTC.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Time { get; }

    public StartupMessageSignalRModel(StartupMessageEntry entry)
    {
        Message = entry.Message;
        Time = entry.Time.ToUniversalTime();
    }
}
