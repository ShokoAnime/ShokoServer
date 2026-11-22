using System;
using System.IO;

namespace Shoko.Tests.Infrastructure;

/// <summary>
///   Removes a test's temporary directory.
/// </summary>
public static class TemporaryDirectory
{
    /// <summary>
    ///   Deletes the directory and what it holds, as far as it can. Windows
    ///   keeps a loaded assembly's file locked until its load context is
    ///   collected, so a failed delete is tried once more after a collection,
    ///   and then left to the system's clean-up of temporary files.
    /// </summary>
    /// <param name="path">The directory.</param>
    public static void Delete(string path)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
