// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using FluentNHibernate.Driver;

namespace Shoko.Server.Databases.SqliteFixes;

public class SqliteDriverFix : MsSQLiteDriver
{
    public override DbConnection CreateConnection()
    {
        var connection = base.CreateConnection();
        connection.StateChange += Connection_StateChange;
        return connection;
    }

    private static void Connection_StateChange(object sender, StateChangeEventArgs e)
    {
        if (e.OriginalState is not (ConnectionState.Broken or ConnectionState.Closed or ConnectionState.Connecting) ||
            e.CurrentState != ConnectionState.Open)
            return;

        var connection = (DbConnection)sender;
        using var command = connection.CreateCommand();
        // Activated foreign keys if supported by SQLite.  Unknown pragmas are ignored.
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();
    }
}
