using System;

namespace Shoko.Server.Renamer;

public record MoveFileResult : IFileOperationResult
{
    public bool IsSuccess { get; set; }
    public bool CanRetry { get; set; }
    public string NewFolder { get; set; }
    public string ErrorMessage { get; set; }
    public Exception Exception { get; set; }
}
