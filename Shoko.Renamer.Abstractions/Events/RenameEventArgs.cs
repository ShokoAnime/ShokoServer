using System.ComponentModel;

namespace Shoko.Renamer.Abstractions
{
    public class RenameEventArgs : CancelEventArgs
    {
        /// <summary>
        /// The final name of the file
        /// </summary>
        public string Result { get; set; }
    }
}