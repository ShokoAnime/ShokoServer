using System;

namespace Shoko.Server.Databases
{
    public class DatabaseCommand
    {
        public int Version { get; }
        public int Revision { get; }
        public string Command { get; }
        public Func<object, Tuple<bool, string>> UpdateCommand { get; }
        public Action DatabaseFix { get; }

        public DatabaseCommandType Type
        {
            get
            {
                if (UpdateCommand != null)
                    return DatabaseCommandType.CodedCommand;
                if (DatabaseFix != null)
                    return DatabaseCommandType.PostDatabaseFix;
                return DatabaseCommandType.NormalCommand;
            }
        }

        public DatabaseCommand(int version, int revision, string command)
        {
            Version = version;
            Revision = revision;
            Command = command;
        }

        public DatabaseCommand(int version, int revision, Action databasefix)
        {
            Version = version;
            Revision = revision;
            DatabaseFix = databasefix;
        }

        public DatabaseCommand(int version, int revision, Func<object, Tuple<bool, string>> updatecommand)
        {
            Version = version;
            Revision = revision;
            UpdateCommand = updatecommand;
        }

        public DatabaseCommand(Func<object, Tuple<bool, string>> updatecommand)
        {
            Version = 0;
            Revision = 0;
            UpdateCommand = updatecommand;
        }

        public DatabaseCommand(string command)
        {
            Command = command;
        }

        public string CommandName
        {
            get
            {
                if (UpdateCommand != null)
                    return "[" + UpdateCommand.Method.Name + "]";
                if (DatabaseFix != null)
                    return "[" + DatabaseFix.Method.Name + "]";
                return Command;
            }
        }
    }

    public enum DatabaseCommandType
    {
        NormalCommand,
        CodedCommand,
        PostDatabaseFix,
    }
}
