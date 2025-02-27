using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using NLog;
using Shoko.Plugin.Abstractions.Hashing;
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

    public static List<HashDigest> CalculateHashes(string strPath, bool getED2K = false, bool getCRC32 = false, bool getMD5 = false, bool getSHA1 = false, bool getSHA256 = false, bool getSHA512 = false, CancellationToken cancellationToken = default)
    {
        // Short circuit if no hashes are requested.
        if (!getED2K && !getCRC32 && !getMD5 && !getSHA1 && !getSHA256 && !getSHA512)
            return [];

        if (Finalize.ModuleHandle == IntPtr.Zero && !Utils.IsLinux)
            return CalculateHashesSlow(strPath, getED2K, getCRC32, getMD5, getSHA1, getSHA256, getSHA512, cancellationToken);

        var hashes = (List<HashDigest>?)null;
        try
        {
            var filename = strPath;
            if (!Utils.IsLinux)
            {
                filename = strPath.StartsWith(@"\\")
                    ? strPath
                    : @"\\?\" + strPath; //only prepend non-UNC paths (or paths that have this already)
            }

            hashes = NativeHasher.GetHash(filename, getED2K, getCRC32, getMD5, getSHA1, getSHA256, getSHA512, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        if (hashes is not null)
            return hashes;

        logger.Error("Error using DLL to get hash, trying C# code instead: {0}", strPath);

        return CalculateHashesSlow(strPath, getED2K, getCRC32, getMD5, getSHA1, getSHA256, getSHA512, cancellationToken);
    }

    private const int ChunkSize = 9728000;

    private static List<HashDigest> CalculateHashesSlow(string strPath, bool getED2K, bool getCRC32, bool getMD5, bool getSHA1, bool getSHA256, bool getSHA512, CancellationToken cancellationToken = default)
    {
        logger.Trace("Using C# code to hash file: {0}", strPath);

        using var stream = File.OpenRead(strPath);
        using var md4 = getED2K ? MD4.Create() : null;
        using var md5 = getMD5 ? MD5.Create() : null;
        using var sha1 = getSHA1 ? SHA1.Create() : null;
        using var sha256 = getSHA256 ? SHA256.Create() : null;
        using var sha512 = getSHA512 ? SHA512.Create() : null;
        using var crc32 = getCRC32 ? new Crc32() : null;
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
            if (cancellationToken.IsCancellationRequested)
                return [];

            chunkCount++;

            logger.Trace("Hashing Chunk: " + chunkCount.ToString());

            var bytesRead = stream.Read(workBuffer, 0, bytesToRead);
            if (md4 is not null)
            {
                var md4Hash = md4.ComputeHash(workBuffer, 0, bytesRead);
                var chunkOffset = (int)((chunkCount - 1) * 16);
                for (var index = 0; index < 16; index++)
                    ed2kHash[chunkOffset + index] = md4Hash[index];
            }
            md5?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            sha1?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            crc32?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            sha256?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            sha512?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            byteOffset += ChunkSize;
            bytesRemaining = totalBytes - byteOffset;
            if (bytesRemaining < ChunkSize)
                bytesToRead = (int)bytesRemaining;
        }

        if (cancellationToken.IsCancellationRequested)
            return [];

        md5?.TransformFinalBlock(workBuffer, 0, 0);
        sha1?.TransformFinalBlock(workBuffer, 0, 0);
        crc32?.TransformFinalBlock(workBuffer, 0, 0);
        sha256?.TransformFinalBlock(workBuffer, 0, 0);
        sha512?.TransformFinalBlock(workBuffer, 0, 0);

        var hashes = new List<HashDigest>();
        if (md4 is not null)
        {
            hashes.Add(new()
            {
                Type = "ED2K",
                Value = numberOfBlocks > 1
                    ? BitConverter.ToString(md4.ComputeHash(ed2kHash)).Replace("-", string.Empty).ToUpper()
                    : BitConverter.ToString(ed2kHash).Replace("-", string.Empty).ToUpper()
            });
        }
        if (crc32 is not null)
            hashes.Add(new() { Type = "CRC32", Value = BitConverter.ToString(crc32.Hash!).Replace("-", string.Empty).ToUpper() });
        if (md5 is not null)
            hashes.Add(new() { Type = "MD5", Value = BitConverter.ToString(md5.Hash!).Replace("-", string.Empty).ToUpper() });
        if (sha1 is not null)
            hashes.Add(new() { Type = "SHA1", Value = BitConverter.ToString(sha1.Hash!).Replace("-", string.Empty).ToUpper() });
        if (sha256 is not null)
            hashes.Add(new() { Type = "SHA256", Value = BitConverter.ToString(sha256.Hash!).Replace("-", string.Empty).ToUpper() });
        if (sha512 is not null)
            hashes.Add(new() { Type = "SHA512", Value = BitConverter.ToString(sha512.Hash!).Replace("-", string.Empty).ToUpper() });
        return hashes;
    }
}
