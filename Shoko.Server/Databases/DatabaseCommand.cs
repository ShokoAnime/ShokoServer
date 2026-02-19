using System;

namespace Shoko.Server.Databases;

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
            {
                return DatabaseCommandType.CodedCommand;
            }

            if (DatabaseFix != null)
            {
                return DatabaseCommandType.PostDatabaseFix;
            }

            return DatabaseCommandType.NormalCommand;
        }
    }

    public DatabaseCommand(int version, int revision, string command)
    {
        Version = version;
        Revision = revision;
        Command = command;
    }

    public DatabaseCommand(int version, int revision, Action databaseFix)
    {
        Version = version;
        Revision = revision;
        DatabaseFix = databaseFix;
    }

    public DatabaseCommand(int version, int revision, Func<object, Tuple<bool, string>> updateCommand = null)
    {
        Version = version;
        Revision = revision;
        UpdateCommand = updateCommand ?? DatabaseFixes.NoOperation;
    }

    public DatabaseCommand(Func<object, Tuple<bool, string>> updateCommand)
    {
        UpdateCommand = updateCommand;
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
            {
                return "[" + UpdateCommand.Method.Name + "]";
            }

            if (DatabaseFix != null)
            {
                return "[" + DatabaseFix.Method.Name + "]";
            }

            return Command;
        }
    }
}

public enum DatabaseCommandType
{
    NormalCommand,
    CodedCommand,
    PostDatabaseFix
}
