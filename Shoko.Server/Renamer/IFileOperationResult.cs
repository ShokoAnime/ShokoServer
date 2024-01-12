using System;

namespace Shoko.Server.Renamer;

public interface IFileOperationResult
{
    bool IsSuccess { get; set; }
    bool CanRetry { get; set; }
    string ErrorMessage { get; set; }
    Exception Exception { get; set; }
}
