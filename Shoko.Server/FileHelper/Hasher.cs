using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NLog;
using Shoko.Server.Utilities;
using Exception = System.Exception;

#nullable enable
namespace Shoko.Server.FileHelper;

public class Hasher
{
    public static Logger logger = LogManager.GetCurrentClassLogger();

    public delegate int OnHashProgress([MarshalAs(UnmanagedType.LPWStr)] string strFileName, int nProgressPct);

    [Flags]
    internal enum LoadLibraryFlags : uint
    {
        DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
        LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
        LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
        LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
        LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
        LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
    }

    [DllImport("kernel32.dll")]
    internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeLibrary(IntPtr hModule);

    private static readonly Destructor Finalize = new(); //static Destructor hack

    internal sealed class Destructor : IDisposable
    {
        public IntPtr ModuleHandle;

        ~Destructor()
        {
            if (ModuleHandle != IntPtr.Zero)
            {
                FreeLibrary(ModuleHandle);
                ModuleHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
        }
    }

    static Hasher()
    {
        if (Utils.IsLinux)
        {
            Finalize.ModuleHandle = IntPtr.Zero;
            return;
        }

        var dllPath = Assembly.GetEntryAssembly()?.Location;
        try
        {
            if (dllPath != null)
            {
                var fi = new FileInfo(dllPath);
                dllPath = Path.Combine(fi.Directory!.FullName, Environment.Is64BitProcess ? "x64" : "x86", "librhash.dll");
                Finalize.ModuleHandle = LoadLibraryEx(dllPath, IntPtr.Zero, 0);
            }
        }
        catch (Exception)
        {
            Finalize.ModuleHandle = IntPtr.Zero;
        }
    }

    public static string? GetVersion()
    {
        if (Utils.IsLinux)
            return null;

        try
        {
            var dllPath = Assembly.GetEntryAssembly()!.Location;
            var fi = new FileInfo(dllPath);
            dllPath = Path.Combine(fi.Directory!.FullName, Environment.Is64BitProcess ? "x64" : "x86", "librhash.dll");

            if (!File.Exists(dllPath)) return null;

            var fvi = FileVersionInfo.GetVersionInfo(dllPath);
            return $"RHash {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({dllPath})";
        }
        catch
        {
            return null;
        }
    }

    public static Hashes CalculateHashes(string strPath, OnHashProgress onHashProgress, bool getCRC32, bool getMD5, bool getSHA1)
    {
        Hashes? hashes = null;
        if (Finalize.ModuleHandle == IntPtr.Zero && !Utils.IsLinux)
            return CalculateHashesSlow(strPath, onHashProgress, getCRC32, getMD5, getSHA1);

        try
        {
            var filename = strPath;
            if (!Utils.IsLinux)
            {
                filename = strPath.StartsWith(@"\\")
                    ? strPath
                    : @"\\?\" + strPath; //only prepend non-UNC paths (or paths that have this already)
            }

            var (ed2k, crc32, md5, sha1) = NativeHasher.GetHash(filename, getCRC32, getMD5, getSHA1);
            if (!string.IsNullOrEmpty(ed2k))
            {
                hashes = new() { ED2K = ed2k };
                if (getCRC32)
                    hashes.CRC32 = crc32;
                if (getMD5)
                    hashes.MD5 = md5;
                if (getSHA1)
                    hashes.SHA1 = sha1;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        if (hashes is not null)
            return hashes;

        logger.Error("Error using DLL to get hash, trying C# code instead: {0}", strPath);

        return CalculateHashesSlow(strPath, onHashProgress, getCRC32, getMD5, getSHA1);
    }

    private const int ChunkSize = 9728000;

    private static Hashes CalculateHashesSlow(string strPath, OnHashProgress onHashProgress, bool getCRC32, bool getMD5, bool getSHA1)
    {
        logger.Trace("Using C# code to hash file: {0}", strPath);

        onHashProgress?.Invoke(strPath, 0);

        var stream = File.OpenRead(strPath);
        var md4 = MD4.Create();
        var md5 = getMD5 ? MD5.Create() : null;
        var sha1 = getSHA1 ? SHA1.Create() : null;
        var crc32 = getCRC32 ? new Crc32() : null;
        var totalBytes = stream.Length;
        var numberOfBlocks = (int)Math.DivRem(totalBytes, ChunkSize, out var remainder);
        if (remainder > 0)
            numberOfBlocks++;

        var bytesRemaining = stream.Length;
        var bytesToRead = totalBytes > ChunkSize
            ? ChunkSize
            : (int)bytesRemaining;
        var ed2kHash = new byte[16 * numberOfBlocks];
        var workBuffer = new byte[bytesToRead];
        var byteOffset = 0L;
        var chunkCount = 0L;
        while (bytesRemaining > 0)
        {
            chunkCount++;

            logger.Trace("Hashing Chunk: " + chunkCount.ToString());

            var bytesRead = stream.Read(workBuffer, 0, bytesToRead);
            var md4Hash = md4.ComputeHash(workBuffer, 0, bytesRead);
            var chunkOffset = (int)((chunkCount - 1) * 16);
            for (var index = 0; index < 16; index++)
                ed2kHash[chunkOffset + index] = md4Hash[index];

            md5?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            sha1?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            crc32?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);

            var percentComplete = (int)(chunkCount / (float)numberOfBlocks * 100);
            onHashProgress?.Invoke(strPath, percentComplete);

            byteOffset += ChunkSize;
            bytesRemaining = totalBytes - byteOffset;
            if (bytesRemaining < ChunkSize)
                bytesToRead = (int)bytesRemaining;
        }

        md5?.TransformFinalBlock(workBuffer, 0, 0);
        sha1?.TransformFinalBlock(workBuffer, 0, 0);
        crc32?.TransformFinalBlock(workBuffer, 0, 0);

        stream.Close();

        onHashProgress?.Invoke(strPath, 100);

        var hashes = new Hashes
        {
            ED2K = numberOfBlocks > 1
                ? BitConverter.ToString(md4.ComputeHash(ed2kHash)).Replace("-", string.Empty).ToUpper()
                : BitConverter.ToString(ed2kHash).Replace("-", string.Empty).ToUpper()
        };
        if (crc32 is not null)
            hashes.CRC32 = BitConverter.ToString(crc32.Hash!).Replace("-", string.Empty).ToUpper();
        if (md5 is not null)
            hashes.MD5 = BitConverter.ToString(md5.Hash!).Replace("-", string.Empty).ToUpper();
        if (sha1 is not null)
            hashes.SHA1 = BitConverter.ToString(sha1.Hash!).Replace("-", string.Empty).ToUpper();
        return hashes;
    }
}
