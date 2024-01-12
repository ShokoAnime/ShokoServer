using System;

namespace Shoko.Server.Renamer;

public record RenameFileResult : IFileOperationResult
{
    public bool IsSuccess { get; set; }
    public bool CanRetry { get; set; }
    public string NewFilename { get; set; }
    public string ErrorMessage { get; set; }
    public Exception Exception { get; set; }
}
