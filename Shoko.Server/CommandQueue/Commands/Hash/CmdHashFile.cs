using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Repositories;

#pragma warning disable 4014

namespace Shoko.Server.CommandQueue.Commands.Hash
{
    public class CmdHashFile : BaseCommand<CmdHashFile>, ICommand
    {
        public bool Force { get; set; }
        public IFile File { get; internal set; }
        public HashTypes Types { get; internal set; }

        public const int BUFFER_SIZE = 65536;

        private int _parallelMax = -1;

        //Same tags can be parallelized up to ParallelMax
        //TODO move somehow this to settings
        //Examples:
        //If FileSystem is Local or Network local, ParallelTag will be the letter of the drive,
        //or the name of the network host (In the future will be nicer, if we get the name of the hard drive instead, hint in linux)
        //For local Filesystem the maximun paralelization is 1.
        //If Cloud, parallel tag will be the name of the account, max parallelization hardcoded to 4.
        // In this way, different drives could be parallelized in hashing.
        private string _parallelTag;
        public virtual QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.HashingFile, extraParams = new[] {File.FullName,Types.ToString("F")}};
        public WorkTypes WorkType => Commands.WorkTypes.Hashing;
        public Dictionary<HashTypes, byte[]> Result { get; set; }

        public virtual int Priority { get; set; } = 5;
        public virtual string Id => $"ServerHashFile_{File.FullName}_{(int)Types}";

        public string GetHashType(HashTypes t)
        {
            if (!Result.ContainsKey(t))
                return null;
            byte[] data = Result.First(a => a.Key == t).Value;
            return BitConverter.ToString(data).Replace("-", string.Empty);
        }

        public string ParallelTag
        {
            get
            {
                if (!string.IsNullOrEmpty(_parallelTag))
                    return _parallelTag;
                if (File.FileSystem.GetType().Name.Contains("LocalFileSystem"))
                {
                    string str = File.FullName.Replace("\\", "/").Replace(":", "");
                    if (str.StartsWith("//"))
                        str = str.Substring(2);
                    int a = str.IndexOf("/",StringComparison.InvariantCulture);
                    if (a > 0)
                        str = str.Substring(0, a);
                    return str;
                }

                return File.FileSystem.Name;
            }
            set { _parallelTag = value; }
        }

        public int ParallelMax
        {
            get
            {
                if (_parallelMax != -1)
                    return _parallelMax;
                if (File.FileSystem.GetType().Name.Contains("LocalFileSystem"))
                    return 1;
                if ((File.FileSystem.Supports & (SupportedFlags.MD5 | SupportedFlags.SHA1)) > 0)
                    return 4;
                return 1;
            }
            set { _parallelMax = value; }
        }
        public CmdHashFile(IFile file, HashTypes ht, bool force)
        {
            File = file;
            Types = ht;
            Force = force;
        }

        public CmdHashFile()
        {

        }
        public CmdHashFile(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                InternalSerialize hf = JsonConvert.DeserializeObject<InternalSerialize>(str);
                IFileSystem fs = Repo.Instance.ImportFolder.GetAll().FirstOrDefault(a => a.FileSystem.Name == hf.FileSystemName)?.FileSystem;
                if (fs != null)
                {
                    IObject obj = fs.Resolve(hf.FullName);
                    if (obj.Status == Status.Ok && obj is IFile file)
                    {
                        File = file;
                        Types = hf.HType;
                        Force = hf.Force;
                    }
                }
            }
        }
        public override string Serialize()
        {
            string str = null;
            if (File != null)
            {
                InternalSerialize hf = new InternalSerialize {FileSystemName = File.FileSystem.Name, FullName = File.FullName, HType = Types, Force = Force};
                str = JsonConvert.SerializeObject(hf, JsonSettings);
            }

            return str;
        }


        [DllImport("hasher.dll", EntryPoint = "Init", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Init(int hashtypes, long filesize);

        [DllImport("hasher.dll", EntryPoint = "Update", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void Update(IntPtr context, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, long size);

        [DllImport("hasher.dll", EntryPoint = "Finish", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void Finish(IntPtr context, byte[] hashes);


        public override async Task<CommandResult> RunAsync(IProgress<ICommandProgress> progress = null, CancellationToken token = default(CancellationToken))
        {
            Task t = null;
            ThreadUnit tu = new ThreadUnit();
            try
            {
                if (File == null)
                    return ReportErrorAndGetResult(progress, CommandResultStatus.Error, "File not found");
                FileSystemResult<Stream> fs = await File.OpenReadAsync();
                if (fs.Status != Status.Ok)
                    return ReportErrorAndGetResult(progress, CommandResultStatus.Error, fs.Error);
                InitProgress(progress);
                tu.WorkUnit = this;
                tu.FileSize = File.Size;
                tu.Buffer = new byte[2][];
                tu.Buffer[0] = new byte[BUFFER_SIZE];
                tu.Buffer[1] = new byte[BUFFER_SIZE];
                tu.BufferNumber = 0;
                t = Task.Factory.StartNew(HashWorker, tu, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
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
                        UpdateAndReportProgress(progress,(double) read * 100 / tu.FileSize);
                        if (tu.Abort)
                            return new CommandResult(CommandResultStatus.Error, tu.Error);
                        tu.MainAutoResetEvent.WaitOne();
                        tu.BufferNumber ^= 1;
                    }
                    catch (OperationCanceledException)
                    {
                        tu.CancelWorker();
                        return ReportErrorAndGetResult(progress, CommandResultStatus.Canceled, "Operation Canceled");
                    }
                    catch (Exception e)
                    {
                        tu.CancelWorker();
                        return ReportErrorAndGetResult(progress, CommandResultStatus.Error, e.Message);
                    }
                } while (tu.CurrentSize != 0);

                if (tu.Abort)
                    return ReportErrorAndGetResult(progress, CommandResultStatus.Error, tu.Error);
                tu.MainAutoResetEvent.WaitOne();
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception e)
            {
                if (t != null)
                    tu.CancelWorker();
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, e.Message, e);
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

        private class InternalSerialize
        {
            public string FileSystemName { get; set; }
            public string FullName { get; set; }
            public HashTypes HType { get; set; }
            public bool Force { get; set; }
        }

        internal class ThreadUnit
        {
            public bool Abort;
            public byte[][] Buffer;
            public int BufferNumber;
            public int CurrentSize;
            public string Error;
            public long FileSize;
            public AutoResetEvent MainAutoResetEvent = new AutoResetEvent(false);
            public AutoResetEvent WorkerAutoResetEvent = new AutoResetEvent(false);
            public CmdHashFile WorkUnit;

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
    }
}