using System;
using Shoko.Models;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_ScanFile : ScanFile
    {
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
