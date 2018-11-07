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
using Shoko.Server.Native.Hashing;
using Shoko.Server.Repositories;

#pragma warning disable 4014

namespace Shoko.Server.CommandQueue.Commands.Hash
{
    public class CmdHashFile : BaseCommand, ICommand
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
        public virtual QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.HashingFile, ExtraParams = new[] {File.FullName,Types.ToString("F")}};
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
                    if (obj.Status == NutzCode.CloudFileSystem.Status.Ok && obj is IFile file)
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




        public override async Task RunAsync(IProgress<ICommand> progress = null, CancellationToken token = default(CancellationToken))
        {
            if (File == null)
            {
                ReportErrorAndGetResult(progress, "File not found");
                return;
            }
            try
            {
                InitProgress(progress);
                Hasher h = new Hasher(File, Types);
                string error = await h.RunAsync(new ChildProgress(0, 100, this, progress), token);
                if (error != null)
                {
                    ReportErrorAndGetResult(progress, CommandStatus.Error, error);
                    return;
                }
                Result = h.Result;
                ReportFinishAndGetResult(progress);
            }
            catch (Exception e)
            {
                ReportErrorAndGetResult(progress, CommandStatus.Error, e.Message, e);
            }
        }


    
        private class InternalSerialize
        {
            public string FileSystemName { get; set; }
            public string FullName { get; set; }
            public HashTypes HType { get; set; }
            public bool Force { get; set; }
        }
    }
}