using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NutzCode.CloudFileSystem;
using Shoko.Server.Repositories;
using Shoko.Server.Workers.WorkUnits.Hashing;

#pragma warning disable 4014

namespace Shoko.Server.Workers.Commands.Hashing
{
    public class CMD_HashFile : IWorkCommand<HashFile>
    {
        [DllImport("hasher.dll", EntryPoint = "Init", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Init(int hashtypes, long filesize);

        [DllImport("hasher.dll", EntryPoint = "Update", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void Update(IntPtr context, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, long size);

        [DllImport("hasher.dll", EntryPoint = "Finish", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void Finish(IntPtr context, byte[] hashes);

        public const int BUFFER_SIZE = 65536;
        public string Name => "File Hasher";

        internal class ThreadUnit
        {
            public HashFile WorkUnit;
            public int BufferNumber;
            public AutoResetEvent MainAutoResetEvent = new AutoResetEvent(false);
            public AutoResetEvent WorkerAutoResetEvent = new AutoResetEvent(false);
            public byte[][] Buffer;
            public int CurrentSize;
            public long FileSize;
            public bool Abort;
            public string Error;

            public void CancelWorker()
            {
                if (!Abort)
                {
                    Abort = true;
                    WorkerAutoResetEvent.Set();
                    MainAutoResetEvent.WaitOne();
                }
            }

            public void WorkerError(string error)
            {
                if (Abort)
                    return;
                Error = error;
                Abort = true;
            }
        }

        public virtual async Task<WorkResult<HashFile>> RunAsync(HashFile workunit, IProgress<IWorkProgress<HashFile>> progress = null, CancellationToken token = default(CancellationToken))
        {
            Task t = null;
            ThreadUnit tu = new ThreadUnit();
            try
            {
                if (workunit.File == null)
                    return new WorkResult<HashFile>(WorkResultStatus.Error, "File not found");
                FileSystemResult<Stream> fs = await workunit.File.OpenReadAsync();
                if (fs.Status!=Status.Ok)
                    return new WorkResult<HashFile>(WorkResultStatus.Error, fs.Error);
                BasicWorkProgress<HashFile> progressdata = new BasicWorkProgress<HashFile>
                {
                    Command = this,
                    Unit = workunit
                };
                tu.WorkUnit = workunit;
                tu.FileSize = workunit.File.Size;
                tu.Buffer = new byte[2][];
                tu.Buffer[0] = new byte[BUFFER_SIZE];
                tu.Buffer[1] = new byte[BUFFER_SIZE];
                tu.BufferNumber = 0;
                t=Task.Factory.StartNew(HashWorker, tu, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
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
                        if (progress != null)
                        {
                            progressdata.Progress = (double) read * 100 / tu.FileSize;
                            progress.Report(progressdata);
                        }
                        if (tu.Abort)
                            return new WorkResult<HashFile>(WorkResultStatus.Error, tu.Error);
                        tu.MainAutoResetEvent.WaitOne();
                        tu.BufferNumber ^= 1;
                    }
                    catch (OperationCanceledException)
                    {
                        tu.CancelWorker();
                        return new WorkResult<HashFile>(WorkResultStatus.Canceled, "Operation Canceled");
                    }
                    catch (Exception e)
                    {
                        tu.CancelWorker();
                        return new WorkResult<HashFile>(WorkResultStatus.Error, e.Message);
                    }
                } while (tu.CurrentSize != 0);
                if (tu.Abort)
                    return new WorkResult<HashFile>(WorkResultStatus.Error, tu.Error);
                tu.MainAutoResetEvent.WaitOne();
                return new WorkResult<HashFile>(tu.WorkUnit);
            }
            catch (Exception e)
            {
                if (t!=null)
                    tu.CancelWorker();
                return new WorkResult<HashFile>(WorkResultStatus.Error, e.Message);
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

        internal class InternalSerialize
        {
            public string FileSystemName { get; set; }
            public string FullName { get; set; }
            public HashTypes Type { get; set; }
            public bool Force { get; set; }
        }

        public virtual async Task<HashFile> DeserializeAsync(string str)
        {
            HashFile ret = null;
            if (!string.IsNullOrEmpty(str))
            {
                InternalSerialize hf = JsonConvert.DeserializeObject<InternalSerialize>(str);
                IFileSystem fs = Repo.Instance.ImportFolder.GetAll().FirstOrDefault(a => a.FileSystem.Name == hf.FileSystemName)?.FileSystem;
                if (fs != null)
                {
                    IObject obj = await fs.ResolveAsync(hf.FullName);
                    if (obj.Status==Status.Ok && obj is IFile file)
                        ret = new HashFile(file, hf.Type,hf.Force);
                }
            }
            //TODO add Logging on errors;
            return await Task.FromResult(ret);
        }

        public virtual async Task<string> SerializeAsync(HashFile workunit)
        {
            string str = null;
            if (workunit?.File != null)
            {
                InternalSerialize hf = new InternalSerialize {FileSystemName = workunit.File.FileSystem.Name, FullName = workunit.File.FullName, Type = workunit.Types, Force=workunit.Force};
                str = JsonConvert.SerializeObject(hf);
            }
            //TODO add Logging on errors;
            return await Task.FromResult(str);
        }
    }
}