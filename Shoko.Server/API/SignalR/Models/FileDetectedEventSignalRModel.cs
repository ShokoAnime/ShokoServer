using Shoko.Plugin.Abstractions;

namespace Shoko.Server.API.SignalR.Models
{
    public class FileDetectedEventSignalRModel
    {
        public FileDetectedEventSignalRModel(FileDetectedEventArgs eventArgs)
        {
            RelativePath = eventArgs.RelativePath;
            ImportFolderID = eventArgs.ImportFolder.ImportFolderID;
        }

        /// <summary>
        /// The relative path of the file from the import folder base location
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// The ID of the import folder the event was detected in.
        /// </summary>
        /// <value></value>
        public int ImportFolderID { get; set; }
    }
}