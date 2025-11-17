using System;

namespace Shoko.Plugin.Abstractions.Relocation;

/// <summary>
/// An error or exception that occurred during a relocation operation.
/// </summary>
public class RelocationError
{
    /// <summary>
    /// The exception that caused the error, if applicable.
    /// </summary>
    public Exception? Exception { get; private set; }

    /// <summary>
    /// Error message. Should always be set.
    /// </summary>

    public string Message { get; private set; }

    /// <summary>
    /// Create a new relocation error.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="exception">Exception that caused the error</param>
    public RelocationError(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }
    /// <summary>
    /// Create a new relocation error.
    /// </summary>
    /// <param name="exception">Exception that caused the error</param>
    public RelocationError(Exception exception)
    {
        Message = exception.Message;
        Exception = exception;
    }
}
