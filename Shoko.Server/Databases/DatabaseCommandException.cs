using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.Databases
{
    [Serializable]
    public class DatabaseCommandException : Exception
    {
        public DatabaseCommand DatabaseCommand { get; set; }

        public DatabaseCommandException(string message, DatabaseCommand cmd) : base(message)
        {
            DatabaseCommand = cmd;
        }

        public override string ToString()
        {
            return "DATABASE ERROR: " + DatabaseCommand.Version + "." + DatabaseCommand.Revision + " " +
                   DatabaseCommand.CommandName + " | " + Message;
        }
    }
}