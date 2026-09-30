using System;
using System.Runtime.InteropServices;

namespace Shoko.Server.Utilities;

/// <summary>
/// Hands memory that native code freed back to the operating system.
/// </summary>
internal static class NativeAllocator
{
    #region Trim

    /// <summary>
    /// Asks the C allocator to return its free memory to the operating system.
    /// Only glibc on Linux keeps it otherwise; everywhere else, and on a C
    /// library without <c>malloc_trim</c>, this does nothing.
    /// </summary>
    public static void Trim()
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            MallocTrim(UIntPtr.Zero);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    [DllImport("libc", EntryPoint = "malloc_trim", CallingConvention = CallingConvention.Cdecl)]
    private static extern int MallocTrim(UIntPtr pad);

    #endregion
}
