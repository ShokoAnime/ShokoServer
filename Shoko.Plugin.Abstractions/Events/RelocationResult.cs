using System.IO;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// The result of a relocation operation by a <see cref="IRenamer"/>.
/// </summary>
public class RelocationResult
{
    /// <summary>
    /// The new filename, without any path.
    /// </summary>
    /// <remarks>
    /// If the file name contains a path then it will be moved to
    /// <see cref="Path"/> if it is unset, or otherwise discarded.
    ///
    /// This shouldn't be null unless a) there was an <see cref="Error"/>, b)
    /// the renamer doesn't support renaming, or c) the rename operation will be
    /// skipped as indicated by <see cref="SkipRename"/>.
    /// </remarks>
    public string? FileName { get; set; }

    /// <summary>
    /// The new path without the <see cref="FileName"/>, relative to the import
    /// folder.
    /// </summary>
    /// <remarks>
    /// Sub-folders should be separated with <see cref="Path.DirectorySeparatorChar"/>
    /// or <see cref="Path.AltDirectorySeparatorChar"/>. This shouldn't be null
    /// unless a) there was an <see cref="Error"/>, b) the renamer doesn't
    /// support moving, or c) the move operation will be skipped as indicated by
    /// <see cref="SkipMove"/>.
    /// </remarks>
    public string? Path { get; set; }

    /// <summary>
    /// The new import folder where the file should live.
    /// </summary>
    /// <remarks>
    /// This should be set from <see cref="RelocationEventArgs.AvailableFolders"/>,
    /// and shouldn't be null unless a) there was an <see cref="Error"/>, b) the
    /// renamer doesn't support moving, or c) the move operation will be skipped
    /// as indicated by <see cref="SkipMove"/>.
    /// </remarks>
    public IImportFolder? DestinationImportFolder { get; set; }

    /// <summary>
    /// Indicates that the result does not contain a path and destination, and
    /// the file <strong>should not</strong> be moved.
    /// </summary>
    public bool SkipMove { get; set; } = false;

    /// <summary>
    /// Indicates that the result does not contain a file name, and the file
    /// <strong>should not</strong> be renamed.
    /// </summary>
    public bool SkipRename { get; set; } = false;

    /// <summary>
    /// Set this object if the event is not successful. If this is set, it
    /// assumed that there was a failure, and the rename/move operation should
    /// be aborted. It is required to have at least a message.
    ///
    /// An exception can be provided if relevant.
    /// </summary>
    public RelocationError? Error { get; set; }
}
