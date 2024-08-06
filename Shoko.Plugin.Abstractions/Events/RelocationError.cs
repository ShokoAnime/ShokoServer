using System;

namespace Shoko.Plugin.Abstractions.Events;

public class RelocationError
{
    public Exception? Exception { get; set; }
    public string Message { get; set; }

    public RelocationError(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }
}
