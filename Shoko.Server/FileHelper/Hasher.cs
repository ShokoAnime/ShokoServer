﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Utilities;
using Exception = System.Exception;

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

    private static readonly Destructor Finalise = new(); //static Destructor hack

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
        var fullexepath = Assembly.GetEntryAssembly().Location;
        try
        {
            if (fullexepath != null)
            {
                var fi = new FileInfo(fullexepath);
                fullexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "librhash.dll");
                Finalise.ModuleHandle = LoadLibraryEx(fullexepath, IntPtr.Zero, 0);
            }
        }
        catch (Exception)
        {
            Finalise.ModuleHandle = IntPtr.Zero;
        }
    }

    #region DLL functions

    [DllImport("hasher.dll", EntryPoint = "CalculateHashes_AsyncIO", CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Unicode)]
    private static extern int CalculateHashes_callback_dll(
        [MarshalAs(UnmanagedType.LPWStr)] string szFileName,
        [MarshalAs(UnmanagedType.LPArray)] byte[] hash,
        [MarshalAs(UnmanagedType.FunctionPtr)] OnHashProgress lpHashProgressFunc,
        [MarshalAs(UnmanagedType.Bool)] bool getCRC32,
        [MarshalAs(UnmanagedType.Bool)] bool getMD5,
        [MarshalAs(UnmanagedType.Bool)] bool getSHA1
    );

    // Calculates hash immediately (with progress)
    protected static int CalculateHashes_dll(string strFileName, ref byte[] hash, OnHashProgress HashProgress,
        bool getCRC32, bool getMD5, bool getSHA1)
    {
        logger.Trace("Using DLL to hash file: {0}", strFileName);
        var pHashProgress = HashProgress;
        var gcHashProgress = GCHandle.Alloc(pHashProgress); //to make sure the GC doesn't dispose the delegate

        return CalculateHashes_callback_dll(strFileName, hash, pHashProgress, getCRC32, getMD5, getSHA1);
    }


    public static string HashToString(byte[] hash, int start, int length)
    {
        if (hash == null || hash.Length == 0)
        {
            return string.Empty;
        }

        var hex = new StringBuilder(length * 2);
        for (var x = start; x < start + length; x++)
        {
            hex.AppendFormat("{0:x2}", hash[x]);
        }

        return hex.ToString().ToUpper();
    }

    #endregion

    public static string GetVersion()
    {
        try
        {
            var fullHasherexepath = Assembly.GetEntryAssembly().Location;
            var fi = new FileInfo(fullHasherexepath);
            fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86", "librhash.dll");

            if (!File.Exists(fullHasherexepath)) return null;

            var fvi = FileVersionInfo.GetVersionInfo(fullHasherexepath);
            return $"RHash {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({fullHasherexepath})";
        }
        catch
        {
            return null;
        }
    }

    public static Hashes CalculateHashes(string strPath, OnHashProgress onHashProgress, bool getCRC32, bool getMD5, bool getSHA1)
    {
        var rhash = new Hashes();
        if (Finalise.ModuleHandle == IntPtr.Zero && !Utils.IsLinux)
            return CalculateHashes_here(strPath, onHashProgress, getCRC32, getMD5, getSHA1);

        var hash = new byte[56];
        var gotHash = false;
        var rval = -1;
        try
        {
            var filename = strPath;
            if (!Utils.IsLinux)
            {
                filename = strPath.StartsWith(@"\\")
                    ? strPath
                    : @"\\?\" + strPath; //only prepend non-UNC paths (or paths that have this already)
            }

            var (e2Dk, crc32, md5, sha1) = NativeHasher.GetHash(filename, getCRC32, getMD5, getSHA1);
            rhash.ED2K = e2Dk;
            if (!string.IsNullOrEmpty(rhash.ED2K)) gotHash = true;
            if (getCRC32) rhash.CRC32 = crc32;
            if (getMD5) rhash.MD5 = md5;
            if (getSHA1) rhash.SHA1 = sha1;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        if (gotHash) return rhash;

        logger.Error("Error using DLL to get hash (Functon returned {0}), trying C# code instead: {0}", rval,
            strPath);

        return CalculateHashes_here(strPath, onHashProgress, getCRC32, getMD5, getSHA1);
    }

    public static Hashes CalculateHashes_here(string strPath, OnHashProgress onHashProgress, bool getCRC32,
        bool getMD5,
        bool getSHA1)
    {
        var getED2k = true;
        logger.Trace("Using C# code to has file: {0}", strPath);

        FileStream fs;
        var rhash = new Hashes();
        var fi = new FileInfo(strPath);
        fs = fi.OpenRead();
        var lChunkSize = 9728000;

        var nBytes = fs.Length;

        var nBytesRemaining = fs.Length;
        var nBytesToRead = 0;

        var nBlocks = nBytes / lChunkSize;
        var nRemainder = nBytes % lChunkSize; //mod
        if (nRemainder > 0)
        {
            nBlocks++;
        }

        var baED2KHash = new byte[16 * nBlocks];

        if (nBytes > lChunkSize)
        {
            nBytesToRead = lChunkSize;
        }
        else
        {
            nBytesToRead = (int)nBytesRemaining;
        }

        onHashProgress?.Invoke(strPath, 0);

        var md4 = MD4.Create();
        var md5 = MD5.Create();
        var sha1 = SHA1.Create();
        var crc32 = new Crc32();

        var ByteArray = new byte[nBytesToRead];

        long iOffSet = 0;
        long iChunkCount = 0;
        while (nBytesRemaining > 0)
        {
            iChunkCount++;

            //logger.Trace("Hashing Chunk: " + iChunkCount.ToString());

            var nBytesRead = fs.Read(ByteArray, 0, nBytesToRead);

            if (getED2k)
            {
                var baHash = md4.ComputeHash(ByteArray, 0, nBytesRead);
                var j = (int)((iChunkCount - 1) * 16);
                for (var i = 0; i < 16; i++)
                {
                    baED2KHash[j + i] = baHash[i];
                }
            }

            if (getMD5)
            {
                md5.TransformBlock(ByteArray, 0, nBytesRead, ByteArray, 0);
            }

            if (getSHA1)
            {
                sha1.TransformBlock(ByteArray, 0, nBytesRead, ByteArray, 0);
            }

            if (getCRC32)
            {
                crc32.TransformBlock(ByteArray, 0, nBytesRead, ByteArray, 0);
            }

            var percentComplete = (int)(iChunkCount / (float)nBlocks * 100);
            onHashProgress?.Invoke(strPath, percentComplete);

            iOffSet += lChunkSize;
            nBytesRemaining = nBytes - iOffSet;
            if (nBytesRemaining < lChunkSize)
            {
                nBytesToRead = (int)nBytesRemaining;
            }
        }

        if (getMD5)
        {
            md5.TransformFinalBlock(ByteArray, 0, 0);
        }

        if (getSHA1)
        {
            sha1.TransformFinalBlock(ByteArray, 0, 0);
        }

        if (getCRC32)
        {
            crc32.TransformFinalBlock(ByteArray, 0, 0);
        }


        fs.Close();

        onHashProgress?.Invoke(strPath, 100);

        if (getED2k)
        {
            //byte[] baHashFinal = md4.ComputeHash(baED2KHash);
            //rhash.ed2k = BitConverter.ToString(baHashFinal).Replace("-", string.Empty).ToUpper();
            rhash.ED2K = nBlocks > 1
                ? BitConverter.ToString(md4.ComputeHash(baED2KHash)).Replace("-", string.Empty).ToUpper()
                : BitConverter.ToString(baED2KHash).Replace("-", string.Empty).ToUpper();
        }

        if (getCRC32)
        {
            rhash.CRC32 = BitConverter.ToString(crc32.Hash).Replace("-", string.Empty).ToUpper();
        }

        if (getMD5)
        {
            rhash.MD5 = BitConverter.ToString(md5.Hash).Replace("-", string.Empty).ToUpper();
        }

        if (getSHA1)
        {
            rhash.SHA1 = BitConverter.ToString(sha1.Hash).Replace("-", string.Empty).ToUpper();
        }

        return rhash;
    }
}
