namespace Shoko.Plugin.Abstractions.DataModels;

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
    /// The rules that this Import Folder should adhere to. E.g. a folder that is both a <see cref="DropFolderType.Source"/> and <see cref="DropFolderType.Destination"/> cares not how files are moved in or out of it.
    /// </summary>
    DropFolderType DropFolderType { get; }
}
