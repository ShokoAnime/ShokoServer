using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   How one chunk of a core-driven sweep ended.
/// </summary>
/// <remarks>
///   Telling <see cref="TimedOut"/>, <see cref="Stopped"/> and
///   <see cref="Cancelled"/> apart is best effort. All three arrive as an
///   <c>OperationCanceledException</c> and the server decides between them by
///   asking which token was cancelled by the time it caught it, so a provider
///   that cancels for its own reason in the same instant the deadline fires is
///   reported as having timed out.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AiringScheduleSweepOutcome : byte
{
    /// <summary>
    ///   The provider returned. The cursor it returned decides whether the
    ///   sweep carries on or is over.
    /// </summary>
    Completed = 0,

    /// <summary>
    ///   The chunk's deadline passed before the provider returned. The stored
    ///   cursor is left as it was, so the next chunk starts where the last one
    ///   the provider actually returned left off.
    /// </summary>
    TimedOut = 1,

    /// <summary>
    ///   The server is shutting down, or the queue was stopped. Nothing about
    ///   this is the provider's fault and it is not held against it.
    /// </summary>
    Stopped = 2,

    /// <summary>
    ///   The provider cancelled the chunk itself, neither the deadline nor the
    ///   shutdown having fired.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    ///   The provider threw. Every other provider's sweep is unaffected.
    /// </summary>
    Failed = 4,
}
