using System;

namespace Shoko.Plugin.Abstractions
{
    public interface IShokoEventHandler
    {
        event EventHandler<FileDetectedEventArgs> FileDetected;
        event EventHandler<FileHashedEventArgs> FileHashed;
        event EventHandler<FileMatchedEventArgs> FileMatched;
    }
}
