using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

public class FileNotMatchedEventSignalRModel : FileEventSignalRModel
{
    public FileNotMatchedEventSignalRModel(FileNotMatchedEventArgs eventArgs) : base(eventArgs)
    {
        AutoMatchAttempts = eventArgs.AutoMatchAttempts;
        HasCrossReferences = eventArgs.HasCrossReferences;
        IsUDPBanned = eventArgs.IsUDPBanned;
    }

    /// <summary>
    /// Number of times we've tried to auto-match this file up until now.
    /// </summary>
    public int AutoMatchAttempts { get; set; }

    /// <summary>
    /// True if this file had existing cross-refernces before this match
    /// attempt.
    /// </summary>
    public bool HasCrossReferences { get; set; }

    /// <summary>
    /// True if we're currently UDP banned.
    /// </summary>
    public bool IsUDPBanned { get; set; }
}
