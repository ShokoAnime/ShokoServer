using System;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Thrown by a stream rendition to refuse a request with a reason the
///   client is told. Throw one of the derived types; the core answers any
///   other with <c>500</c>.
/// </summary>
/// <param name="reason">Why the request was refused, sent to the client.</param>
/// <param name="innerException">The exception that caused the refusal, if any.</param>
public class StreamResourceException(string reason, Exception? innerException = null) : Exception(reason, innerException);
