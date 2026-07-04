using System.IO;

namespace Shoko.Server.Services.FileSystemWatcher;

public class FileSystemWatcherLockOptions
{
    public bool Enabled { get; set; } = true;
    public FileAccess FileAccessMode { get; set; } = FileAccess.Read;
    public bool Aggressive { get; set; }
    public int WaitTimeMilliseconds { get; set; }
    public int AggressiveWaitTimeSeconds { get; set; }
}
