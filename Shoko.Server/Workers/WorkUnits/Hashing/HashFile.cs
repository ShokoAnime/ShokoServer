using System.Collections.Generic;
using NutzCode.CloudFileSystem;
using Shoko.Server.Models;

namespace Shoko.Server.Workers.WorkUnits.Hashing
{
    public class HashFile : IWorkUnit
    {
        public string Info => File.FullName;
        public IFile File { get;  }
        public HashTypes Types { get; }
        public Dictionary<HashTypes,byte[]> Result { get; set; }
        public bool Force { get; set; }

        public HashFile(IFile file, HashTypes ht, bool force)
        {
            File = file;
            Types = ht;
            Force = force;
        }
    }
}
