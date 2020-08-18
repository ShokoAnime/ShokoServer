namespace Shoko.Plugin.Abstractions.DataModels
{
    public interface IImportFolder
    {
        /// <summary>
        /// The Base Location of the Import Folder in the host, VM, or container filesystem
        /// </summary>
        string Location { get; set; }

        /// <summary>
        /// The rules that this Import Folder should adhere to. A folder that is both a Source and Destination cares not how files are moved in or out of it.
        /// </summary>
        DropFolderType DropFolderType { get; set; }
    }
}