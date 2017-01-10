using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Shoko.Models;
using NLog;
using Shoko.Models.Server;
using Path = Pri.LongPath.Path;

namespace Shoko.Server.FileHelper
{
    public class Hasher
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public delegate int OnHashProgress([MarshalAs(UnmanagedType.LPWStr)] string strFileName, int nProgressPct);

        [System.Flags]
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

        private static readonly Destructor Finalise = new Destructor(); //static Destructor hack

        internal sealed class Destructor
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
        }

        static Hasher()
        {
            string fullexepath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            FileInfo fi = new FileInfo(fullexepath);
            fullexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86", "hasher.dll");
            try
            {
                Finalise.ModuleHandle = LoadLibraryEx(fullexepath, IntPtr.Zero, 0);
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
        protected static bool CalculateHashes_dll(string strFileName, ref byte[] hash, OnHashProgress HashProgress,
            bool getCRC32, bool getMD5, bool getSHA1)
        {
            logger.Trace("Using DLL to hash file: {0}", strFileName);
            OnHashProgress pHashProgress = new OnHashProgress(HashProgress);
            GCHandle gcHashProgress = GCHandle.Alloc(pHashProgress); //to make sure the GC doesn't dispose the delegate

            int nResult = CalculateHashes_callback_dll(strFileName, hash, pHashProgress, getCRC32, getMD5, getSHA1);

            return nResult == 0;
        }


        public static string HashToString(byte[] hash, int start, int length)
        {
            if (hash == null || hash.Length == 0)
                return string.Empty;

            StringBuilder hex = new StringBuilder(length*2);
            for (int x = start; x < start + length; x++)
            {
                hex.AppendFormat("{0:x2}", hash[x]);
            }
            return hex.ToString().ToUpper();
        }

        #endregion

        public static Hashes CalculateHashes(string strPath, OnHashProgress onHashProgress)
        {
            return CalculateHashes(strPath, onHashProgress, true, true, true);
        }

        public static Hashes CalculateHashes(string strPath, OnHashProgress onHashProgress, bool getCRC32, bool getMD5,
            bool getSHA1)
        {
            Hashes rhash = new Hashes();
            if (Finalise.ModuleHandle != IntPtr.Zero)
            {
                byte[] hash = new byte[56];
                bool gotHash = false;
                try
                {
                    if (CalculateHashes_dll(strPath, ref hash, onHashProgress, getCRC32, getMD5, getSHA1))
                    {
                        rhash.ED2K = HashToString(hash, 0, 16);
                        if (!string.IsNullOrEmpty(rhash.ED2K)) gotHash = true;
                        if (getCRC32) rhash.CRC32 = HashToString(hash, 16, 4);
                        if (getMD5) rhash.MD5 = HashToString(hash, 20, 16);
                        if (getSHA1) rhash.SHA1 = HashToString(hash, 36, 20);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error( ex,ex.ToString());
                }

                if (!gotHash)
                {
                    logger.Error("Error using DLL to get hash (Functon returned FALSE), trying C# code instead: {0}",
                        strPath);
                    return CalculateHashes_here(strPath, onHashProgress, getCRC32, getMD5, getSHA1);
                }
                return rhash;
            }
            return CalculateHashes_here(strPath, onHashProgress, getCRC32, getMD5, getSHA1);
        }

        public static Hashes CalculateHashes_here(string strPath, OnHashProgress onHashProgress, bool getCRC32,
            bool getMD5,
            bool getSHA1)
        {
            bool getED2k = true;
            logger.Trace("Using C# code to has file: {0}", strPath);

            FileStream fs;
            Hashes rhash = new Hashes();
            FileInfo fi = new FileInfo(strPath);
            fs = fi.OpenRead();
            int lChunkSize = 9728000;

            long nBytes = (long) fs.Length;

            long nBytesRemaining = (long) fs.Length;
            int nBytesToRead = 0;

            long nBlocks = nBytes/lChunkSize;
            long nRemainder = nBytes%lChunkSize; //mod
            if (nRemainder > 0)
                nBlocks++;

            byte[] baED2KHash = new byte[16*nBlocks];

            if (nBytes > lChunkSize)
                nBytesToRead = lChunkSize;
            else
                nBytesToRead = (int) nBytesRemaining;

            if (onHashProgress != null)
                onHashProgress(strPath, 0);

            MD4 md4 = MD4.Create();
            MD5 md5 = MD5.Create();
            SHA1 sha1 = SHA1.Create();
            Crc32 crc32 = new Crc32();

            byte[] ByteArray = new byte[nBytesToRead];

            long iOffSet = 0;
            long iChunkCount = 0;
            while (nBytesRemaining > 0)
            {
                iChunkCount++;

                //logger.Trace("Hashing Chunk: " + iChunkCount.ToString());

                int nBytesRead = fs.Read(ByteArray, 0, nBytesToRead);

                if (getED2k)
                {
                    byte[] baHash = md4.ComputeHash(ByteArray, 0, nBytesRead);
                    int j = (int) ((iChunkCount - 1)*16);
                    for (int i = 0; i < 16; i++)
                        baED2KHash[j + i] = baHash[i];
                }

                if (getMD5) md5.TransformBlock(ByteArray, 0, nBytesRead, ByteArray, 0);
                if (getSHA1) sha1.TransformBlock(ByteArray, 0, nBytesRead, ByteArray, 0);
                if (getCRC32) crc32.TransformBlock(ByteArray, 0, nBytesRead, ByteArray, 0);

                int percentComplete = (int) ((float) iChunkCount/(float) nBlocks*100);
                if (onHashProgress != null)
                    onHashProgress(strPath, percentComplete);

                iOffSet += lChunkSize;
                nBytesRemaining = nBytes - iOffSet;
                if (nBytesRemaining < lChunkSize)
                    nBytesToRead = (int) nBytesRemaining;
            }
            if (getMD5) md5.TransformFinalBlock(ByteArray, 0, 0);
            if (getSHA1) sha1.TransformFinalBlock(ByteArray, 0, 0);
            if (getCRC32) crc32.TransformFinalBlock(ByteArray, 0, 0);


            fs.Close();

            if (onHashProgress != null)
                onHashProgress(strPath, 100);

            if (getED2k)
            {
                //byte[] baHashFinal = md4.ComputeHash(baED2KHash);
                //rhash.ed2k = BitConverter.ToString(baHashFinal).Replace("-", "").ToUpper();
                rhash.ED2K = nBlocks > 1
                    ? BitConverter.ToString(md4.ComputeHash(baED2KHash)).Replace("-", "").ToUpper()
                    : BitConverter.ToString(baED2KHash).Replace("-", "").ToUpper();
            }
            if (getCRC32) rhash.CRC32 = BitConverter.ToString(crc32.Hash).Replace("-", "").ToUpper();
            if (getMD5) rhash.MD5 = BitConverter.ToString(md5.Hash).Replace("-", "").ToUpper();
            if (getSHA1) rhash.SHA1 = BitConverter.ToString(sha1.Hash).Replace("-", "").ToUpper();
            return rhash;
        }
    }
}