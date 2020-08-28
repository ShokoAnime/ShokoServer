namespace Shoko.Plugin.Abstractions.DataModels
{
    public interface IImportFolder
    {
        /// <summary>
        /// The Import Folder's name. This is user specified in WebUI, or NA for legacy
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// The Base Location of the Import Folder in the host, VM, or container filesystem
        /// </summary>
        string Location { get; }

        /// <summary>
        /// The rules that this Import Folder should adhere to. A folder that is both a Source and Destination cares not how files are moved in or out of it.
        /// </summary>
        DropFolderType DropFolderType { get; }
    }
}