using System;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions
{

    public class FileEventArgs : EventArgs
    {
        /// <summary>
        /// The relative path of the file from the ImportFolder base location
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Information about the file itself, such as media info or hashes.
        /// </summary>
        public IVideoFile FileInfo { get; set; }

        /// <summary>
        /// The import folder that the file is in
        /// </summary>
        public IImportFolder ImportFolder { get; set; }
    }
}