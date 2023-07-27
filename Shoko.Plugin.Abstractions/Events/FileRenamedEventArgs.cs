
namespace Shoko.Plugin.Abstractions
{
    public class FileRenamedEventArgs : FileEventArgs
    {
        /// <summary>
        /// The new Filename, after the rename
        /// </summary>
        public string NewFileName { get; set; }

        /// <summary>
        /// The old Filename, before we renamed it
        /// </summary>
        public string OldFileName { get; set; }
    }
}
