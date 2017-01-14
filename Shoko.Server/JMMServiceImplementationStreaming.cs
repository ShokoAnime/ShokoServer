using System;
using System.IO;
using Shoko.Models;
using Shoko.Models.Server;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Models.Interfaces;
using Shoko.Server.Entities;

namespace Shoko.Server
{
    public class JMMServiceImplementationStreaming : IJMMServerStreaming
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public System.IO.Stream Download(string fileName)
        {
            try
            {
                IFile file = SVR_VideoLocal.ResolveFile(fileName);
                if (file == null)
                    return null;
                FileSystemResult<Stream> r=file.OpenRead();
                if (!r.IsOk)
                    return null;
                return r.Result;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }
    }
}