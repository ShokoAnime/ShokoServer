using System;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Thrown by a stream rendition when the requested resource is still being
///   produced, so the player retries instead of the request waiting. The core
///   answers <c>503</c>, with a <c>Retry-After</c> header when
///   <see cref="RetryAfter"/> is set.
/// </summary>
/// <param name="reason">Why the request was refused, sent to the client.</param>
/// <param name="retryAfter">How long the client should wait before retrying, if known.</param>
/// <param name="innerException">The exception that caused the refusal, if any.</param>
public sealed class StreamResourceNotReadyException(string reason, TimeSpan? retryAfter = null, Exception? innerException = null) : StreamResourceException(reason, innerException)
{
    /// <summary>
    ///   How long the client should wait before retrying, sent as
    ///   <c>Retry-After</c> in whole seconds, rounded up.
    /// </summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
