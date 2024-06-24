using System;

namespace Shoko.Plugin.Abstractions;

public class MoveRenameError
{
    public Exception? Exception { get; set; }
    public string Message { get; set; }

    public MoveRenameError(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }
}
