using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions;

public class RelocationResult
{
    /// <summary>
    /// The new filename, without any path. This shouldn't be null unless there was an <see cref="Error"/> or the renamer doesn't support renaming.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// The new path, without the <see cref="FileName"/>, relative to the import folder. Subfolders should be separated with <see cref="System.IO.Path.DirectorySeparatorChar"/>. This shouldn't be null unless there was an <see cref="Error"/> or the renamer doesn't support moving.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// The new Import Folder for the file to moved to. This should be set from <see cref="RelocationEventArgs.AvailableFolders"/>. This shouldn't be null unless there was an <see cref="Error"/> or the renamer doesn't support moving.
    /// </summary>
    public IImportFolder? DestinationImportFolder { get; set; }

    /// <summary>
    /// Set this object if the event is not successful. If this is set, it assumed that there was a failure, and the rename/move operation should be aborted. It is required to have at least a message.
    /// An exception can be provided if relevant.
    /// </summary>
    public MoveRenameError? Error { get; set; }
}
