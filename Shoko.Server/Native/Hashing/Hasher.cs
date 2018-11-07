using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Server.Utilities;

#pragma warning disable 4014

namespace Shoko.Server.Native.Hashing
{
    public partial class Hasher
    {
        [DllImport("hasher.dll", EntryPoint = "Init", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Init(int hashtypes, long filesize);

        [DllImport("hasher.dll", EntryPoint = "Update", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void Update(IntPtr context, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, long size);

        [DllImport("hasher.dll", EntryPoint = "Finish", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void Finish(IntPtr context, byte[] hashes);

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






        public const int BUFFER_SIZE = 65536;
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        internal static IntPtr Handle;

        // ReSharper disable once UnusedMember.Local
        private static readonly Destructor _ = new Destructor();

        static Hasher()
        {
            #region Shoko

            if (Handle == IntPtr.Zero && !Utils.IsLinux)
            {
                string fullexepath = Assembly.GetEntryAssembly().Location;
                if (!string.IsNullOrEmpty(fullexepath))
                {
                    FileInfo fi = new FileInfo(fullexepath);
                    // ReSharper disable once PossibleNullReferenceException
                    fullexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86", "hasher.dll");

                    logger.Info("Using Hasher at: {0}", fullexepath);

                    Handle = LoadLibraryEx(fullexepath, IntPtr.Zero, 0);
                    if (Handle == IntPtr.Zero)
                    {
                        logger.Warn("Unable to load Hasher.dll, using fallback slower hasher");
                        UsingFallbackHasher = true;
                    }
                }
            }

            #endregion
        }

        public Hasher(IFile file, HashTypes ht = HashTypes.CRC | HashTypes.ED2K | HashTypes.MD5 | HashTypes.SHA1)
        {
            File = file;
            Types = ht;
        }

        public IFile File { get; }
        public HashTypes Types { get; }

        public Dictionary<HashTypes, byte[]> Result { get; set; } = new Dictionary<HashTypes, byte[]>();


        public static bool UsingFallbackHasher { get; set; } = false;
        
        public async Task<string> RunAsync(IProgress<double> progress = null, CancellationToken token = default(CancellationToken))
        {
            Task t = null;
            ThreadUnit tu = new ThreadUnit();
            try
            {
                if (File == null)
                    return "File not found";
                Action<object> hasher_func;
                if (UsingFallbackHasher)
                    hasher_func = Fallback.FallbackHasher.HashWorker;
                else
                    hasher_func = HashWorker;
                FileSystemResult<Stream> fs = await File.OpenReadAsync();
                if (fs.Status != Status.Ok)
                    return fs.Error;
                progress?.Report(0);
                tu.WorkUnit = this;
                tu.FileSize = File.Size;
                tu.Buffer = new byte[2][];
                tu.Buffer[0] = new byte[BUFFER_SIZE];
                tu.Buffer[1] = new byte[BUFFER_SIZE];
                tu.BufferNumber = 0;
               
                t = Task.Factory.StartNew(hasher_func, tu, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
                long read = 0;
                do
                {
                    try
                    {
                        tu.CurrentSize = await fs.Result.ReadAsync(tu.Buffer[tu.BufferNumber], 0, BUFFER_SIZE, token);
                        read += tu.CurrentSize;
                        if (token.IsCancellationRequested)
                            token.ThrowIfCancellationRequested();
                        tu.WorkerAutoResetEvent.Set();
                        progress?.Report((double) read * 100 / tu.FileSize);
                        if (tu.Abort)
                            return "Operation Canceled";
                        tu.MainAutoResetEvent.WaitOne();
                        tu.BufferNumber ^= 1;
                    }
                    catch (OperationCanceledException)
                    {
                        tu.CancelWorker();
                        return "Operation Canceled";
                    }
                    catch (Exception e)
                    {
                        tu.CancelWorker();
                        logger.Error(e, e.Message);
                        return e.Message;
                    }
                } while (tu.CurrentSize != 0);

                if (tu.Abort)
                    return tu.Error;
                tu.MainAutoResetEvent.WaitOne();
                progress?.Report(100);
                return null;
            }
            catch (Exception e)
            {
                if (t != null)
                    tu.CancelWorker();
                logger.Error(e, e.Message);
                return e.Message;
            }
        }


        private static void HashWorker(object f)
        {
            ThreadUnit tu = (ThreadUnit) f;
            IntPtr handle = Init((int) tu.WorkUnit.Types, tu.FileSize);
            if (handle == IntPtr.Zero)
            {
                tu.WorkerError("Unable to Init Hash (failed Init)");
                return;
            }

            do
            {
                tu.WorkerAutoResetEvent.WaitOne();
                if (tu.Abort)
                {
                    tu.MainAutoResetEvent.Set();
                    return;
                }

                int bufferposition = tu.BufferNumber;
                int size = tu.CurrentSize;
                tu.MainAutoResetEvent.Set();
                try
                {
                    if (size != 0)
                    {
                        Update(handle, tu.Buffer[bufferposition], size);
                    }
                    else
                    {
                        byte[] returnhash = new byte[16 + 4 + 16 + 20];
                        Finish(handle, returnhash);
                        Dictionary<HashTypes, byte[]> hashes = new Dictionary<HashTypes, byte[]>();
                        int pos = 0;
                        if ((tu.WorkUnit.Types & HashTypes.ED2K) == HashTypes.ED2K)
                        {
                            byte[] buf = new byte[16];
                            Array.Copy(returnhash, pos, buf, 0, 16);
                            hashes.Add(HashTypes.ED2K, buf);
                            pos += 16;
                        }

                        if ((tu.WorkUnit.Types & HashTypes.CRC) == HashTypes.CRC)
                        {
                            byte[] buf = new byte[4];
                            Array.Copy(returnhash, pos, buf, 0, 4);
                            hashes.Add(HashTypes.CRC, buf);
                            pos += 4;
                        }

                        if ((tu.WorkUnit.Types & HashTypes.MD5) == HashTypes.MD5)
                        {
                            byte[] buf = new byte[16];
                            Array.Copy(returnhash, pos, buf, 0, 16);
                            hashes.Add(HashTypes.MD5, buf);
                            pos += 4;
                        }

                        if ((tu.WorkUnit.Types & HashTypes.SHA1) == HashTypes.SHA1)
                        {
                            byte[] buf = new byte[20];
                            Array.Copy(returnhash, pos, buf, 0, 20);
                            hashes.Add(HashTypes.SHA1, buf);
                        }

                        tu.WorkUnit.Result = hashes;
                        tu.MainAutoResetEvent.Set();
                        return;
                    }
                }
                catch (Exception e)
                {
                    tu.WorkerError(e.Message);
                }
            } while (tu.CurrentSize != 0);
        }

        private sealed class Destructor
        {
            ~Destructor()
            {
                if (Handle != IntPtr.Zero)
                {
                    FreeLibrary(Handle);
                    Handle = IntPtr.Zero;
                }
            }
        }


    }
}