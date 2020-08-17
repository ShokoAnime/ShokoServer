using System;

namespace Shoko.Renamer.Abstractions
{
    /// <summary>
    /// A renamer must implement this to be called
    /// </summary>
    public interface IRenamer
    {
        void GetFilename(RenameEventArgs args);

        void GetDestination(MoveEventArgs args);
    }
}