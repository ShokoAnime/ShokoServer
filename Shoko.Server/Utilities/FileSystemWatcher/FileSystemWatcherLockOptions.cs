// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Shoko.Server.Utilities.FileSystemWatcher;

public class FileSystemWatcherLockOptions
{
    public bool Enabled { get; set; } = true;
    public FileAccess FileAccessMode { get; set; } = FileAccess.Read;
    public bool Aggressive { get; set; }
    public int WaitTimeMilliseconds { get; set; }
    public int AggressiveWaitTimeSeconds { get; set; }
}
