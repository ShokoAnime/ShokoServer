using System;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Thrown by a stream rendition when it cannot serve what the client asked
///   for, e.g. a codec the container cannot carry. The core answers
///   <c>400</c>.
/// </summary>
/// <remarks>
///   The core never treats a bare <see cref="NotSupportedException"/> as the
///   client's fault, since stream plumbing throws it for its own reasons; wrap
///   one in this type where it is known to be about the request.
/// </remarks>
/// <param name="reason">Why the request was refused, sent to the client.</param>
/// <param name="innerException">The exception that caused the refusal, if any.</param>
public sealed class StreamResourceUnsupportedException(string reason, Exception? innerException = null) : StreamResourceException(reason, innerException);
