using System;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Thrown by a stream rendition when the requested resource does not exist,
///   e.g. a track the file does not have. The core answers <c>404</c>.
/// </summary>
/// <param name="reason">Why the request was refused, sent to the client.</param>
/// <param name="innerException">The exception that caused the refusal, if any.</param>
public sealed class StreamResourceNotFoundException(string reason, Exception? innerException = null) : StreamResourceException(reason, innerException);
