using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Entities
{
    public class ScanFile
    {
        public int ScanFileID { get; private set; }
        public int ScanID { get; set; }
        public int ImportFolderID { get; set; }
        public int VideoLocal_Place_ID { get; set; }
        public string FullName { get; set; }
        public long FileSize { get; set; }
        public int Status { get; set; }
        public DateTime? CheckDate { get; set; }
        public string Hash { get; set; }
        public string HashResult { get; set; }


        public ScanFileStatus ScanFileStatus  => (ScanFileStatus)Status;

        public string StatusText
        {
            get
            {
                switch (ScanFileStatus)
                {
                    case ScanFileStatus.Waiting:
                        return "Waiting";
                    case ScanFileStatus.ErrorFileNotFound:
                        return "File Not Found";
                    case ScanFileStatus.ErrorInvalidHash:
                        return "Hash do not match";
                    case ScanFileStatus.ErrorInvalidSize:
                        return "Size do not match";
                    case ScanFileStatus.ErrorMissingHash:
                        return "Missing Hash";
                    case ScanFileStatus.ErrorIOError:
                        return "I/O Error";
                    default:
                        return "Processed";
                }
            }
        }
    }
}
