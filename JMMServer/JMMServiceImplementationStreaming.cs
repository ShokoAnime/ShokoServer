using System;
using System.IO;
using JMMContracts;
using NLog;

namespace JMMServer
{
    public class JMMServiceImplementationStreaming : IJMMServerStreaming
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public Stream Download(string fileName)
        {
            try
            {
                if (!File.Exists(fileName)) return null;

                return File.Open(fileName, FileMode.Open, FileAccess.Read);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }
    }
}