
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions
{
    public class FileMovedEventArgs
    {
        /// <summary>
        /// Information about the file itself, such as media info or hashes.
        /// </summary>
        public IVideoFile FileInfo { get; set; }

        /// <summary>
        /// The new import folder that the file is in
        /// </summary>
        public IImportFolder NewImportFolder { get; set; }

        /// <summary>
        /// The new relative path of the file from the ImportFolder base location
        /// </summary>
        public string NewRelativePath { get; set; }

        /// <summary>
        /// The old import folder that the file was in
        /// </summary>
        public IImportFolder OldImportFolder { get; set; }

        /// <summary>
        /// The old relative path of the file from the old ImportFolder base location
        /// </summary>
        public string OldRelativePath { get; set; }
    }
}
