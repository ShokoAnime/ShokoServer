using System;

namespace Shoko.Server.Exceptions
{
    public class CloudFilesystemException : Exception
    {
        public CloudFilesystemException()
        {
        }

        public CloudFilesystemException(string message) : base(message)
        {
        }

        public CloudFilesystemException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}