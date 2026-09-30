using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks the database of the <see cref="TestPlugins"/> plugin that has one: migrated in the late
/// start, in its own folder, and closed to the plugin until then.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PluginDatabaseTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void ThePluginDatabaseIsMigratedInTheLateStartBeforeThePluginCanUseIt()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var folder = Path.Join(fixture.Services.GetRequiredService<IApplicationPaths>().DatabasePath, TestPlugins.DatabasePluginID.ToString());
        var file = Path.Join(folder, $"{TestPlugins.DatabaseName}.db3");
        Assert.True(File.Exists(file), $"{file} was not created.");

        using (var connection = Open(file))
        {
            Assert.Equal(TestPlugins.DatabaseMigrations, Query(connection, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId"));
            Assert.Equal(["wal"], Query(connection, "PRAGMA journal_mode"));
            Assert.Contains("Tag", Query(connection, "SELECT name FROM pragma_table_info('Notes')"));
        }

        // What the plugin got asking for its database in Ready, and once the start-up was about to complete.
        var plugin = AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == TestPlugins.DatabasePluginAssembly)
            .GetType($"{TestPlugins.DatabasePluginAssembly}.Plugin", throwOnError: true)!;
        Assert.Equal(nameof(InvalidOperationException), plugin.GetProperty("OnReady")!.GetValue(null));
        Assert.Equal($"{TestPlugins.DatabaseMigrations.Length} migrations", plugin.GetProperty("OnAboutToStart")!.GetValue(null));
    }

    private static SqliteConnection Open(string file)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static List<string> Query(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
            values.Add(reader.GetString(0));
        return values;
    }
}
