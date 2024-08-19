namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents an import folder.
/// </summary>
public interface IImportFolder
{
    /// <summary>
    /// The id of the import folder.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// The friendly name of the import folder.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// An absolute path leading to the root of the import folder.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// The available free space in the import folder.
    /// </summary>
    /// <remarks>
    /// A value of <code>-1</code> indicates that the the import folder does not
    /// exist, while a value of <code>-2</code> indicates that free space could
    /// not be determined.
    /// </remarks>
    long AvailableFreeSpace { get; }

    /// <summary>
    /// The rules that this Import Folder should adhere to. E.g. a folder that is both a <see cref="DropFolderType.Source"/> and <see cref="DropFolderType.Destination"/> cares not how files are moved in or out of it.
    /// </summary>
    DropFolderType DropFolderType { get; }

    /// <summary>
    /// Whether or not the import folder can accept the given <paramref name="file"/>
    /// being moved into it.
    /// </summary>
    /// <remarks>
    /// If the file is already in the import folder then it will always return <code>true</code>.
    /// </remarks>
    /// <param name="file">Video file location.</param>
    /// <returns>A boolean indicating whether or not the file can be accepted being moved into the import folder.</returns>
    bool CanAcceptFile(IVideoFile? file);
}
