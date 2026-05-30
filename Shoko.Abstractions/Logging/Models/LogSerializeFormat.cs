using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Serialization layout for a <see cref="LogEntry"/> and for log downloads.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum LogSerializeFormat
{
    /// <summary>
    ///   Simple one-line text per entry.
    /// </summary>
    [EnumMember(Value = "simple")]
    [JsonStringEnumMemberName("simple")]
    Simple = 0,

    /// <summary>
    ///   More verbose one-line text per entry.
    /// </summary>
    [EnumMember(Value = "full")]
    [JsonStringEnumMemberName("full")]
    Full = 1,

    /// <summary>
    ///   NDJSON (<c>application/x-ndjson</c>) for JSONL sources.
    /// </summary>
    [EnumMember(Value = "json")]
    [JsonStringEnumMemberName("json")]
    Json = 2,

    /// <summary>
    ///   Legacy log layout.
    /// </summary>
    [EnumMember(Value = "legacy")]
    [JsonStringEnumMemberName("legacy")]
    Legacy = 3,

    /// <summary>
    ///   Console layout.
    /// </summary>
    [EnumMember(Value = "console")]
    [JsonStringEnumMemberName("console")]
    Console = 4,
}
