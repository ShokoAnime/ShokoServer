using System;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.Attributes
{
    public class CommandAttribute : Attribute
    {
        public CommandRequestType RequestType { get; }

        public CommandAttribute(CommandRequestType requestType) => RequestType = requestType;
    }
}
