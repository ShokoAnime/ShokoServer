using System;
using System.IO;
using JMMContracts;
using JMMServer.Entities;
using NLog;
using NutzCode.CloudFileSystem;

namespace JMMServer
{
    public class JMMServiceImplementationStreaming : IJMMServerStreaming
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public System.IO.Stream Download(string fileName)
        {
            try
            {
                IFile file = VideoLocal.ResolveFile(fileName);
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