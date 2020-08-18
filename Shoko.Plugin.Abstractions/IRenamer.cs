using System;

namespace Shoko.Plugin.Abstractions
{
    /// <summary>
    /// A renamer must implement this to be called
    /// </summary>
    public interface IRenamer : IPlugin
    {
        void GetFilename(RenameEventArgs args);

        void GetDestination(MoveEventArgs args);
    }
}