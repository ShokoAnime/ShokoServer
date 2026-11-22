using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services.FileSystemWatcher;

/// <summary>
///   The paths a file watcher ignores for now, because the server itself is
///   changing them: held while a change runs, and for a while after.
/// </summary>
/// <remarks>
///   A change is reported after it happens, and on a network share (SMB,
///   NFS) the report can come late or in batches. So a hold is only lifted
///   <see cref="GracePeriod"/> after it is released: <see cref="LocalGracePeriod"/>
///   on a local disk, <see cref="NetworkGracePeriod"/> on a share. Holds are counted per
///   path, so two changes to one path at once do not lift each other's.
/// </remarks>
/// <param name="gracePeriod">How long a released hold still stands.</param>
internal sealed class WatcherExclusions(TimeSpan gracePeriod)
{
    /// <summary>
    ///   For a local disk, whose reports only trail the change slightly.
    /// </summary>
    public static readonly TimeSpan LocalGracePeriod = TimeSpan.FromSeconds(2);

    /// <summary>
    ///   For a network share, long enough for its late or batched reports.
    /// </summary>
    public static readonly TimeSpan NetworkGracePeriod = TimeSpan.FromSeconds(45);

    private readonly Dictionary<string, int> _holds = new(StringComparer.Ordinal);

    /// <summary>
    ///   How long a released hold still stands.
    /// </summary>
    public TimeSpan GracePeriod { get; } = gracePeriod;

    /// <summary>
    ///   Whether any path is held, to skip the lookup when none is.
    /// </summary>
    public bool Any
    {
        get
        {
            lock (_holds)
                return _holds.Count > 0;
        }
    }

    /// <summary>
    ///   Whether the path is held now.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns><c>true</c> when changes to it are ignored.</returns>
    public bool Contains(string path)
    {
        lock (_holds)
            return _holds.ContainsKey(path);
    }

    /// <summary>
    ///   Holds a path.
    /// </summary>
    /// <param name="path">The full path.</param>
    public void Hold(string path)
    {
        lock (_holds)
            _holds[path] = _holds.GetValueOrDefault(path) + 1;
    }

    /// <summary>
    ///   Releases a hold, which is lifted after the grace period.
    /// </summary>
    /// <param name="path">The full path.</param>
    public void Release(string path)
    {
        // Housekeeping, so it never runs as the caller who happened to release.
        using (DetachedFlow.Suppress())
            _ = Task.Delay(GracePeriod, CancellationToken.None).ContinueWith(_ => Lift(path), TaskScheduler.Default);
    }

    private void Lift(string path)
    {
        lock (_holds)
        {
            if (!_holds.TryGetValue(path, out var count))
                return;
            if (count > 1)
                _holds[path] = count - 1;
            else
                _holds.Remove(path);
        }
    }
}
