namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents a file system folder/directory managed by Shoko.
/// </summary>
public interface IManagedFolder
{
    /// <summary>
    ///   The ID of the managed folder.
    /// </summary>
    int ID { get; }

    /// <summary>
    ///   The friendly name of the managed folder.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   An absolute path leading to the root of the managed folder. OS
    ///   dependent. Will always have a trailing slash.
    /// </summary>
    string Path { get; }

    /// <summary>
    ///   The available free space in bytes on the mount/drive where the folder
    ///   is located.
    /// </summary>
    /// <remarks>
    ///   A value of <code>-1</code> indicates that the managed folder does not
    ///   exist, while a value of <code>-2</code> indicates that free space
    ///   could not be determined.
    /// </remarks>
    long AvailableFreeSpace { get; }

    /// <summary>
    ///   Whether or not the managed folder should be watched for new files.
    /// </summary>
    bool WatchForNewFiles { get; }

    /// <summary>
    ///   The rules that this managed folder should adhere to within the file
    ///   renaming/movement system. IF set to anything other than
    ///   <see cref="DropFolderType.Excluded"/> then we need read-write access
    ///   to the folder.
    /// </summary>
    /// <remarks>
    ///   E.g. a folder that is both a <see cref="DropFolderType.Source"/> and
    ///   <see cref="DropFolderType.Destination"/> cares not how files are moved
    ///   in or out of it.
    /// </remarks>
    DropFolderType DropFolderType { get; }
}
