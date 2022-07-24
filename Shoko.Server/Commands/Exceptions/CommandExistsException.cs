using System;

namespace Shoko.Server.Commands.Exceptions
{
    public class CommandExistsException : Exception
    {
        public string CommandID { get; set; }
    }
}
