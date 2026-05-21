using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Databases;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.CLI;

internal static class DatabaseConverterCommand
{
    private enum SourceDatabaseType
    {
        SqlServer,
        MySql,
    }

    private static readonly HashSet<string> ExcludedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Versions",
    };

    private static readonly HashSet<string> LegacyMigratedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "AniDB_Vote",
        "CrossRef_AniDB_TvDBV2",
    };

    private static readonly string[] LegacyUnifiedImageTables =
    [
        "TMDB_Image",
        "TMDB_Image_Entity",
        "AniDB_Anime_PreferredImage",
        "AniDB_Episode_PreferredImage",
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        var options = ParseArgs(args);
        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.SourceConnectionString) || string.IsNullOrWhiteSpace(options.TargetFile))
        {
            PrintUsage();
            return 1;
        }

        try
        {
            await ConvertAsync(options.SourceType, options.SourceConnectionString, options.TargetFile, options.Overwrite);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database conversion failed: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task ConvertAsync(SourceDatabaseType sourceType, string sourceConnectionString, string targetFile, bool overwrite)
    {
        var fullTargetPath = Path.GetFullPath(targetFile);
        if (File.Exists(fullTargetPath))
        {
            if (!overwrite)
            {
                throw new InvalidOperationException($"Target file already exists: {fullTargetPath}. Use --overwrite to replace it.");
            }

            File.Delete(fullTargetPath);
        }

        var targetDirectory = Path.GetDirectoryName(fullTargetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await using var source = await OpenSourceConnectionAsync(sourceType, sourceConnectionString);
        await EnsureSourceVersionSupportedAsync(source, sourceType);

        var targetConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullTargetPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        await InitializeSqliteDatabaseAsync(targetConnectionString);

        await using var target = new SqliteConnection(targetConnectionString);
        await target.OpenAsync();

        await ConfigureSqliteAsync(target);

        var sourceTables = await GetSourceTablesAsync(source, sourceType);
        await PruneLegacyTargetTablesAsync(target, sourceTables);
        var targetTables = await GetSqliteTablesAsync(target);
        var tablesToCopy = targetTables
            .Where(sourceTables.Contains)
            .Where(tableName => !ExcludedTables.Contains(tableName))
            .ToList();
        var sourceOnlyTables = sourceTables
            .Except(targetTables)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetOnlyTables = targetTables
            .Except(sourceTables)
            .Where(tableName => !ExcludedTables.Contains(tableName))
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"Source tables found: {sourceTables.Count}");
        Console.WriteLine($"Target tables found: {targetTables.Count}");
        Console.WriteLine($"Tables to copy: {tablesToCopy.Count}");

        if (sourceOnlyTables.Count > 0)
        {
            Console.WriteLine("Source-only tables not present in the generated SQLite schema:");
            foreach (var skippedTable in sourceOnlyTables)
            {
                Console.WriteLine($"  {skippedTable}");
            }
        }

        if (targetOnlyTables.Count > 0)
        {
            Console.WriteLine("Target-only tables missing from the source database:");
            foreach (var targetOnlyTable in targetOnlyTables)
            {
                Console.WriteLine($"  {targetOnlyTable}");
            }

            await ReportSourceObjectsAsync(source, sourceType, targetOnlyTables);
        }

        if (ExcludedTables.Count > 0)
        {
            Console.WriteLine("Excluded control tables:");
            foreach (var excludedTable in ExcludedTables.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {excludedTable}");
            }
        }

        foreach (var tableName in tablesToCopy)
        {
            await CopyTableAsync(source, sourceType, target, tableName);
        }

        await VerifyCopyAsync(source, sourceType, target, tablesToCopy);
        Console.WriteLine($"Conversion completed successfully: {fullTargetPath}");
    }

    private static async Task EnsureSourceVersionSupportedAsync(DbConnection source, SourceDatabaseType sourceType)
    {
        var sourceVersion = await GetSourceDatabaseVersionAsync(source, sourceType);
        if (sourceVersion is null)
        {
            throw new InvalidOperationException("The source database does not contain a current Database version entry in Versions. Upgrade it with the matching Shoko Server build before conversion.");
        }

        var expectedVersion = await GetExpectedSourceDatabaseVersionAsync(sourceType);
        if (sourceVersion.Value.Version != expectedVersion.Version || sourceVersion.Value.Revision != expectedVersion.Revision)
        {
            throw new InvalidOperationException(
                $"Unsupported source database version for {GetSourceTypeDisplayName(sourceType)}. " +
                $"Found {sourceVersion.Value.Version}.{sourceVersion.Value.Revision} " +
                $"(program: {sourceVersion.Value.Program ?? "unknown"}), expected {expectedVersion.Version}.{expectedVersion.Revision} " +
                $"for this Shoko build. Upgrade the source database with the matching Shoko Server build before conversion.");
        }
    }

    private static string GetSourceTypeDisplayName(SourceDatabaseType sourceType)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => "SQL Server",
            SourceDatabaseType.MySql => "MySQL/MariaDB",
            _ => sourceType.ToString(),
        };
    }

    private static async Task InitializeSqliteDatabaseAsync(string connectionString)
    {
        await using var bootstrapHome = new TemporaryShokoHomeScope();

        var systemService = new SystemService();
        var settings = Utils.SettingsProvider.GetSettings();
        settings.Database.Type = Shoko.Server.Server.Constants.DatabaseType.SQLite;
        settings.Database.OverrideConnectionString = connectionString;
        var pluginManager = GetRequiredPrivateField<PluginManager>(systemService, "_pluginManager");
        InitializeCorePluginOnly(pluginManager, systemService);

        using var host = CreateBootstrapHost(systemService, settings);
        Utils.ServiceContainer = host.Services;
        pluginManager.InitPlugins();

        var databaseFactory = host.Services.GetRequiredService<DatabaseFactory>();
        var repositoryFactory = host.Services.GetRequiredService<RepoFactory>();
        if (!RunInitializeDatabase(systemService, databaseFactory, repositoryFactory))
        {
            throw new InvalidOperationException(systemService.StartupMessage ?? "Shoko database bootstrap failed.");
        }

        databaseFactory.CloseSessionFactory();
    }

    private static async Task ConfigureSqliteAsync(SqliteConnection connection)
    {
        var commands = new[]
        {
            "PRAGMA foreign_keys = OFF;",
            "PRAGMA journal_mode = WAL;",
            "PRAGMA synchronous = OFF;",
        };

        foreach (var commandText in commands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<DbConnection> OpenSourceConnectionAsync(SourceDatabaseType sourceType, string connectionString)
    {
        DbConnection connection = sourceType switch
        {
            SourceDatabaseType.SqlServer => new SqlConnection(connectionString),
            SourceDatabaseType.MySql => new MySqlConnection(connectionString),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };

        await connection.OpenAsync();
        return connection;
    }

    private static async Task<DatabaseVersionInfo?> GetSourceDatabaseVersionAsync(DbConnection connection, SourceDatabaseType sourceType)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => await GetSqlServerDatabaseVersionAsync((SqlConnection)connection),
            SourceDatabaseType.MySql => await GetMySqlDatabaseVersionAsync((MySqlConnection)connection),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static async Task<DatabaseVersionInfo?> GetSqlServerDatabaseVersionAsync(SqlConnection connection)
    {
        const string sql = """
            SELECT TOP 1 VersionValue, VersionRevision, VersionProgram
            FROM Versions
            WHERE VersionType = 'Database'
            ORDER BY TRY_CONVERT(int, VersionValue) DESC, TRY_CONVERT(int, VersionRevision) DESC
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DatabaseVersionInfo(
            ParseVersionPart(reader.GetString(0), "VersionValue"),
            ParseVersionPart(reader.GetString(1), "VersionRevision"),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<DatabaseVersionInfo?> GetMySqlDatabaseVersionAsync(MySqlConnection connection)
    {
        const string sql = """
            SELECT VersionValue, VersionRevision, VersionProgram
            FROM Versions
            WHERE VersionType = 'Database'
            ORDER BY CAST(VersionValue AS SIGNED) DESC, CAST(VersionRevision AS SIGNED) DESC
            LIMIT 1
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DatabaseVersionInfo(
            ParseVersionPart(reader.GetString(0), "VersionValue"),
            ParseVersionPart(reader.GetString(1), "VersionRevision"),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<DatabaseVersionInfo> GetExpectedSourceDatabaseVersionAsync(SourceDatabaseType sourceType)
    {
        await using var bootstrapHome = new TemporaryShokoHomeScope();
        var systemService = new SystemService();
        object database = sourceType switch
        {
            SourceDatabaseType.SqlServer => new SQLServer(systemService),
            SourceDatabaseType.MySql => new MySQL(systemService),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };

        var latest = GetDatabaseCommands(database)
            .Where(command => command.Version > 0)
            .Select(command => new DatabaseVersionInfo(command.Version, command.Revision, null))
            .OrderByDescending(command => command.Version)
            .ThenByDescending(command => command.Revision)
            .First();

        return latest;
    }

    private static IEnumerable<DatabaseCommand> GetDatabaseCommands(object database)
    {
        foreach (var field in database.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            var value = field.GetValue(database);
            if (value is DatabaseCommand singleCommand)
            {
                yield return singleCommand;
                continue;
            }

            if (value is not IEnumerable enumerable)
            {
                continue;
            }

            foreach (var item in enumerable)
            {
                if (item is DatabaseCommand command)
                {
                    yield return command;
                }
            }
        }
    }

    private static int ParseVersionPart(string value, string columnName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Invalid {columnName} value in Versions: {value}");
        }

        return parsed;
    }

    private static async Task CopyTableAsync(DbConnection source, SourceDatabaseType sourceType, SqliteConnection target, string tableName)
    {
        var sourceColumns = await GetSourceColumnsAsync(source, sourceType, tableName);
        var targetColumns = await GetSqliteColumnsAsync(target, tableName);
        var sourceColumnSet = sourceColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceBackedColumns = targetColumns.Where(column => sourceColumnSet.Contains(column.Name)).ToList();
        var fallbackColumns = targetColumns
            .Where(column => !sourceColumnSet.Contains(column.Name) && column.NotNull && string.IsNullOrWhiteSpace(column.DefaultValue))
            .ToList();
        var insertColumns = sourceBackedColumns
            .Concat(fallbackColumns)
            .ToList();

        if (insertColumns.Count == 0)
        {
            Console.WriteLine($"Skipping {tableName}: no shared columns.");
            return;
        }

        if (fallbackColumns.Count > 0)
        {
            Console.WriteLine($"Applying fallback values for {tableName}: {string.Join(", ", fallbackColumns.Select(column => column.Name))}");
        }

        await using var transaction = target.BeginTransaction();
        await using (var deleteCommand = target.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = $"DELETE FROM {QuoteSqliteIdentifier(tableName)};";
            await deleteCommand.ExecuteNonQueryAsync();
        }

        var selectSql = $"SELECT {string.Join(", ", sourceBackedColumns.Select(column => QuoteSourceIdentifier(sourceType, column.Name)))} FROM {QuoteSourceIdentifier(sourceType, tableName)};";
        await using var selectCommand = source.CreateCommand();
        selectCommand.CommandText = selectSql;
        await using var reader = await selectCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

        await using var insertCommand = target.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            $"INSERT INTO {QuoteSqliteIdentifier(tableName)} ({string.Join(", ", insertColumns.Select(column => QuoteSqliteIdentifier(column.Name)))}) VALUES ({string.Join(", ", insertColumns.Select((_, index) => $"@p{index}"))});";

        for (var index = 0; index < insertColumns.Count; index++)
        {
            insertCommand.Parameters.Add(new SqliteParameter($"@p{index}", DbType.Object));
        }

        var rowCount = 0;
        while (await reader.ReadAsync())
        {
            for (var index = 0; index < sourceBackedColumns.Count; index++)
            {
                var value = await reader.IsDBNullAsync(index) ? DBNull.Value : reader.GetValue(index);
                insertCommand.Parameters[index].Value = NormalizeValue(value);
            }

            for (var index = 0; index < fallbackColumns.Count; index++)
            {
                var column = fallbackColumns[index];
                insertCommand.Parameters[sourceBackedColumns.Count + index].Value = GetFallbackValue(tableName, column);
            }

            await insertCommand.ExecuteNonQueryAsync();
            rowCount++;
        }

        await transaction.CommitAsync();
        Console.WriteLine($"Copied {tableName}: {rowCount} rows, {insertColumns.Count} columns.");
    }

    private static async Task VerifyCopyAsync(DbConnection source, SourceDatabaseType sourceType, SqliteConnection target, IReadOnlyList<string> tablesToCopy)
    {
        Console.WriteLine("Verifying migrated data...");

        foreach (var tableName in tablesToCopy)
        {
            var sourceColumns = await GetSourceColumnsAsync(source, sourceType, tableName);
            var targetColumns = await GetSqliteColumnsAsync(target, tableName);
            var sharedColumns = targetColumns
                .Where(column => sourceColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase))
                .Select(column => column.Name)
                .ToList();

            if (sharedColumns.Count == 0)
            {
                Console.WriteLine($"Verified {tableName}: skipped, no shared columns.");
                continue;
            }

            var sourceCount = await GetRowCountAsync(source, sourceType, tableName);
            var targetCount = await GetRowCountAsync(target, tableName);
            if (sourceCount != targetCount)
            {
                throw new InvalidOperationException($"Verification failed for {tableName}: row count mismatch. Source={sourceCount}, Target={targetCount}.");
            }

            await VerifyTableContentAsync(source, sourceType, target, tableName, sharedColumns);

            Console.WriteLine($"Verified {tableName}: {sourceCount} rows, {sharedColumns.Count} shared columns.");
        }
    }

    private static async Task<long> GetRowCountAsync(DbConnection connection, SourceDatabaseType sourceType, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteSourceIdentifier(sourceType, tableName)};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<long> GetRowCountAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteSqliteIdentifier(tableName)};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task VerifyTableContentAsync(DbConnection source, SourceDatabaseType sourceType, SqliteConnection target, string tableName, IReadOnlyList<string> sharedColumns)
    {
        var sourcePrimaryKeys = await GetSourcePrimaryKeyColumnsAsync(source, sourceType, tableName);
        var targetPrimaryKeys = await GetSqlitePrimaryKeyColumnsAsync(target, tableName);
        var sourceColumnTypes = await GetSourceColumnTypesAsync(source, sourceType, tableName);
        var orderColumns = sourcePrimaryKeys
            .Where(column => targetPrimaryKeys.Contains(column, StringComparer.OrdinalIgnoreCase))
            .Where(column => sharedColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (orderColumns.Count == 0)
        {
            orderColumns = sharedColumns
                .Where(column => sourceColumnTypes.TryGetValue(column, out var type) && IsSourceOrderableType(sourceType, type))
                .ToList();
        }

        if (orderColumns.Count == 0)
        {
            throw new InvalidOperationException($"Verification failed for {tableName}: no primary key or SQL-orderable shared columns are available for deterministic comparison.");
        }

        var sourceSql = BuildSourceOrderedSelectSql(sourceType, tableName, sharedColumns, orderColumns, sourceColumnTypes);
        var targetSql = BuildSqliteOrderedSelectSql(tableName, sharedColumns, orderColumns);

        await using var sourceCommand = source.CreateCommand();
        sourceCommand.CommandText = sourceSql;
        await using var sourceReader = await sourceCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        await using var targetCommand = target.CreateCommand();
        targetCommand.CommandText = targetSql;
        await using var targetReader = await targetCommand.ExecuteReaderAsync();

        var rowNumber = 0L;
        while (true)
        {
            var sourceHasRow = await sourceReader.ReadAsync();
            var targetHasRow = await targetReader.ReadAsync();
            if (sourceHasRow != targetHasRow)
            {
                throw new InvalidOperationException($"Verification failed for {tableName}: row stream length mismatch near row {rowNumber + 1}.");
            }

            if (!sourceHasRow)
            {
                return;
            }

            rowNumber++;
            var sourceRow = new string[sharedColumns.Count];
            var targetRow = new string[sharedColumns.Count];
            for (var index = 0; index < sharedColumns.Count; index++)
            {
                sourceRow[index] = NormalizeComparisonValue(await sourceReader.IsDBNullAsync(index) ? null : sourceReader.GetValue(index));
                targetRow[index] = NormalizeComparisonValue(targetReader.IsDBNull(index) ? null : targetReader.GetValue(index));
            }

            for (var index = 0; index < sharedColumns.Count; index++)
            {
                var sourceValue = sourceRow[index];
                var targetValue = targetRow[index];
                if (string.Equals(sourceValue, targetValue, StringComparison.Ordinal))
                {
                    continue;
                }

                var keyDescription = string.Join(", ", orderColumns.Select(column =>
                {
                    var keyIndex = sharedColumns.IndexOf(column);
                    var keyValue = keyIndex >= 0 ? sourceRow[keyIndex] : "<missing>";
                    return $"{column}={keyValue}";
                }));
                throw new InvalidOperationException($"Verification failed for {tableName}: column {sharedColumns[index]} mismatch at row {rowNumber} ({keyDescription}). Source={sourceValue}, Target={targetValue}.");
            }
        }
    }

    private static void AppendValue(List<byte> buffer, object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                buffer.AddRange("NULL"u8.ToArray());
                break;
            case byte[] bytes:
                buffer.AddRange(Convert.ToHexString(bytes).Select(c => (byte)c));
                break;
            case string text when TryNormalizeTemporalString(text, out var normalizedText):
                buffer.AddRange(Encoding.UTF8.GetBytes(normalizedText));
                break;
            case DateTime dateTime:
                buffer.AddRange(Encoding.UTF8.GetBytes(NormalizeDateTime(dateTime)));
                break;
            case DateTimeOffset offset:
                buffer.AddRange(Encoding.UTF8.GetBytes(offset.ToString("O", CultureInfo.InvariantCulture)));
                break;
            case bool boolean:
                buffer.AddRange(boolean ? "1"u8.ToArray() : "0"u8.ToArray());
                break;
            case IFormattable formattable:
                buffer.AddRange(Encoding.UTF8.GetBytes(formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty));
                break;
            default:
                buffer.AddRange(Encoding.UTF8.GetBytes(value.ToString() ?? string.Empty));
                break;
        }
    }

    private static object NormalizeValue(object value)
    {
        return value switch
        {
            DBNull => DBNull.Value,
            Guid guid => guid.ToString(),
            DateTimeOffset offset => offset.UtcDateTime,
            _ => value,
        };
    }

    private static string NormalizeDateTime(DateTime value)
    {
        if (value.TimeOfDay == TimeSpan.Zero)
        {
            return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static bool TryNormalizeTemporalString(string value, out string normalized)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
        {
            normalized = NormalizeDateTime(dateTime);
            return true;
        }

        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            normalized = dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static async Task<HashSet<string>> GetSourceTablesAsync(DbConnection connection, SourceDatabaseType sourceType)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => await GetSqlServerTablesAsync((SqlConnection)connection),
            SourceDatabaseType.MySql => await GetMySqlTablesAsync((MySqlConnection)connection),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static async Task<HashSet<string>> GetSqlServerTablesAsync(SqlConnection connection)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            """;

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<HashSet<string>> GetMySqlTablesAsync(MySqlConnection connection)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_TYPE = 'BASE TABLE'
            """;

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<string>> GetSqliteTablesAsync(SqliteConnection connection)
    {
        const string sql = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """;

        var tables = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<string>> GetSourceColumnsAsync(DbConnection connection, SourceDatabaseType sourceType, string tableName)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => await GetSqlServerColumnsAsync((SqlConnection)connection, tableName),
            SourceDatabaseType.MySql => await GetMySqlColumnsAsync((MySqlConnection)connection, tableName),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static async Task<List<string>> GetSqlServerColumnsAsync(SqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<string>> GetMySqlColumnsAsync(MySqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<string>> GetSourcePrimaryKeyColumnsAsync(DbConnection connection, SourceDatabaseType sourceType, string tableName)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => await GetSqlServerPrimaryKeyColumnsAsync((SqlConnection)connection, tableName),
            SourceDatabaseType.MySql => await GetMySqlPrimaryKeyColumnsAsync((MySqlConnection)connection, tableName),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static async Task<List<string>> GetSqlServerPrimaryKeyColumnsAsync(SqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
              AND TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<string>> GetMySqlPrimaryKeyColumnsAsync(MySqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName
              AND CONSTRAINT_NAME = 'PRIMARY'
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<Dictionary<string, string>> GetSourceColumnTypesAsync(DbConnection connection, SourceDatabaseType sourceType, string tableName)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => await GetSqlServerColumnTypesAsync((SqlConnection)connection, tableName),
            SourceDatabaseType.MySql => await GetMySqlColumnTypesAsync((MySqlConnection)connection, tableName),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static async Task<Dictionary<string, string>> GetSqlServerColumnTypesAsync(SqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
            """;

        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        return columns;
    }

    private static async Task<Dictionary<string, string>> GetMySqlColumnTypesAsync(MySqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName
            """;

        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        return columns;
    }

    private static async Task<List<string>> GetSqlitePrimaryKeyColumnsAsync(SqliteConnection connection, string tableName)
    {
        var columns = new List<(int Position, string Name)>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteSqliteLiteral(tableName)});";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var position = reader.GetInt32(5);
            if (position > 0)
            {
                columns.Add((position, reader.GetString(1)));
            }
        }

        return columns.OrderBy(column => column.Position).Select(column => column.Name).ToList();
    }

    private static async Task ReportSourceObjectsAsync(DbConnection connection, SourceDatabaseType sourceType, IReadOnlyList<string> objectNames)
    {
        switch (sourceType)
        {
            case SourceDatabaseType.SqlServer:
                await ReportSqlServerObjectsAsync((SqlConnection)connection, objectNames);
                return;
            case SourceDatabaseType.MySql:
                await ReportMySqlObjectsAsync((MySqlConnection)connection, objectNames);
                return;
            default:
                throw new InvalidOperationException($"Unsupported source database type: {sourceType}");
        }
    }

    private static async Task ReportSqlServerObjectsAsync(SqlConnection connection, IReadOnlyList<string> objectNames)
    {
        const string sql = """
            SELECT s.name AS SchemaName, o.name AS ObjectName, o.type AS ObjectType, o.type_desc AS ObjectTypeDescription
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.name = @objectName
            ORDER BY s.name, o.type_desc
            """;

        Console.WriteLine("Source object lookup for target-only names:");
        foreach (var objectName in objectNames)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@objectName", objectName);
            await using var reader = await command.ExecuteReaderAsync();
            if (!reader.HasRows)
            {
                Console.WriteLine($"  {objectName}: not found in sys.objects");
                continue;
            }

            while (await reader.ReadAsync())
            {
                Console.WriteLine($"  {objectName}: schema={reader.GetString(0)}, type={reader.GetString(2)}, type_desc={reader.GetString(3)}");
            }
        }
    }

    private static async Task ReportMySqlObjectsAsync(MySqlConnection connection, IReadOnlyList<string> objectNames)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @objectName
            ORDER BY TABLE_TYPE, TABLE_NAME
            """;

        Console.WriteLine("Source object lookup for target-only names:");
        foreach (var objectName in objectNames)
        {
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@objectName", objectName);
            await using var reader = await command.ExecuteReaderAsync();
            if (!reader.HasRows)
            {
                Console.WriteLine($"  {objectName}: not found in information_schema.tables");
                continue;
            }

            while (await reader.ReadAsync())
            {
                Console.WriteLine($"  {objectName}: schema={reader.GetString(0)}, type={reader.GetString(2)}");
            }
        }
    }

    private static object GetFallbackValue(string tableName, SqliteColumnInfo column)
    {
        if (string.Equals(tableName, "CrossRef_File_Episode", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(column.Name, "CrossRefSource", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var normalizedType = (column.Type ?? string.Empty).ToUpperInvariant();
        if (normalizedType.Contains("INT", StringComparison.Ordinal) ||
            normalizedType.Contains("REAL", StringComparison.Ordinal) ||
            normalizedType.Contains("NUM", StringComparison.Ordinal) ||
            normalizedType.Contains("DEC", StringComparison.Ordinal) ||
            normalizedType.Contains("BOOL", StringComparison.Ordinal))
        {
            return 0;
        }

        if (normalizedType.Contains("DATE", StringComparison.Ordinal) || normalizedType.Contains("TIME", StringComparison.Ordinal))
        {
            return new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        if (normalizedType.Contains("BLOB", StringComparison.Ordinal))
        {
            return Array.Empty<byte>();
        }

        return string.Empty;
    }

    private static async Task<List<SqliteColumnInfo>> GetSqliteColumnsAsync(SqliteConnection connection, string tableName)
    {
        var columns = new List<SqliteColumnInfo>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteSqliteLiteral(tableName)});";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new()
            {
                Name = reader.GetString(1),
                Type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                NotNull = reader.GetInt32(3) != 0,
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
            });
        }

        return columns;
    }

    private static async Task PruneLegacyTargetTablesAsync(SqliteConnection target, IReadOnlySet<string> sourceTables)
    {
        var targetTables = await GetSqliteTablesAsync(target);
        foreach (var tableName in LegacyMigratedTables)
        {
            if (sourceTables.Contains(tableName) || !targetTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            await using var command = target.CreateCommand();
            command.CommandText = $"DROP TABLE {QuoteSqliteIdentifier(tableName)};";
            await command.ExecuteNonQueryAsync();
            Console.WriteLine($"Pruned legacy target-only table: {tableName}");
        }
    }

    private static string BuildSourceOrderedSelectSql(SourceDatabaseType sourceType, string tableName, IReadOnlyList<string> selectColumns, IReadOnlyList<string> orderColumns, IReadOnlyDictionary<string, string> columnTypes)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => BuildSqlServerOrderedSelectSql(tableName, selectColumns, orderColumns, columnTypes),
            SourceDatabaseType.MySql => BuildMySqlOrderedSelectSql(tableName, selectColumns, orderColumns, columnTypes),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static string BuildSqlServerOrderedSelectSql(string tableName, IReadOnlyList<string> selectColumns, IReadOnlyList<string> orderColumns, IReadOnlyDictionary<string, string> columnTypes)
    {
        var selectList = string.Join(", ", selectColumns.Select(column => BuildSqlServerSelectExpression(column, columnTypes)));
        var orderList = string.Join(", ", orderColumns.Select(column => BuildSqlServerOrderExpression(column, columnTypes)));
        return $"SELECT {selectList} FROM {QuoteSqlServerIdentifier(tableName)} ORDER BY {orderList};";
    }

    private static string BuildMySqlOrderedSelectSql(string tableName, IReadOnlyList<string> selectColumns, IReadOnlyList<string> orderColumns, IReadOnlyDictionary<string, string> columnTypes)
    {
        var selectList = string.Join(", ", selectColumns.Select(column => BuildMySqlSelectExpression(column, columnTypes)));
        var orderList = string.Join(", ", orderColumns.Select(column => BuildMySqlOrderExpression(column, columnTypes)));
        return $"SELECT {selectList} FROM {QuoteMySqlIdentifier(tableName)} ORDER BY {orderList};";
    }

    private static string BuildSqliteOrderedSelectSql(string tableName, IReadOnlyList<string> selectColumns, IReadOnlyList<string> orderColumns)
    {
        return $"SELECT {string.Join(", ", selectColumns.Select(QuoteSqliteIdentifier))} FROM {QuoteSqliteIdentifier(tableName)} ORDER BY {string.Join(", ", orderColumns.Select(QuoteSqliteIdentifier))};";
    }

    private static string BuildSqlServerSelectExpression(string columnName, IReadOnlyDictionary<string, string> columnTypes)
    {
        var expression = BuildSqlServerOrderExpression(columnName, columnTypes);
        return $"{expression} AS {QuoteSqlServerIdentifier(columnName)}";
    }

    private static string BuildSqlServerOrderExpression(string columnName, IReadOnlyDictionary<string, string> columnTypes)
    {
        var identifier = QuoteSqlServerIdentifier(columnName);
        if (columnTypes.TryGetValue(columnName, out var type) &&
            type.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase))
        {
            return $"LOWER(CONVERT(varchar(36), {identifier}))";
        }

        return identifier;
    }

    private static string BuildMySqlSelectExpression(string columnName, IReadOnlyDictionary<string, string> columnTypes)
    {
        return QuoteMySqlIdentifier(columnName);
    }

    private static string BuildMySqlOrderExpression(string columnName, IReadOnlyDictionary<string, string> columnTypes)
    {
        return QuoteMySqlIdentifier(columnName);
    }

    private static bool IsSourceOrderableType(SourceDatabaseType sourceType, string dataType)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => IsSqlServerOrderableType(dataType),
            SourceDatabaseType.MySql => IsMySqlOrderableType(dataType),
            _ => false,
        };
    }

    private static bool IsSqlServerOrderableType(string dataType)
        => !dataType.Equals("text", StringComparison.OrdinalIgnoreCase) &&
           !dataType.Equals("ntext", StringComparison.OrdinalIgnoreCase) &&
           !dataType.Equals("image", StringComparison.OrdinalIgnoreCase);

    private static bool IsMySqlOrderableType(string dataType)
        => !dataType.Contains("blob", StringComparison.OrdinalIgnoreCase) &&
           !dataType.Equals("json", StringComparison.OrdinalIgnoreCase) &&
           !dataType.Equals("geometry", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeComparisonValue(object? value)
    {
        return value switch
        {
            null or DBNull => "<null>",
            byte[] bytes => Convert.ToHexString(bytes),
            string text when TryNormalizeTemporalString(text, out var normalizedText) => normalizedText,
            DateTime dateTime => NormalizeDateTime(dateTime),
            DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "1" : "0",
            sbyte number => NormalizeNumericValue(number),
            byte number => NormalizeNumericValue(number),
            short number => NormalizeNumericValue(number),
            ushort number => NormalizeNumericValue(number),
            int number => NormalizeNumericValue(number),
            uint number => NormalizeNumericValue(number),
            long number => NormalizeNumericValue(number),
            ulong number => NormalizeNumericValue(number),
            decimal number => NormalizeNumericValue(number),
            double number => NormalizeNumericValue(number),
            float number => NormalizeNumericValue(number),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string NormalizeNumericValue<T>(T value)
        where T : struct, ISpanFormattable
    {
        return value switch
        {
            decimal decimalValue => decimalValue.ToString("G29", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
            _ => value.ToString(null, CultureInfo.InvariantCulture),
        };
    }

    private static string QuoteSqlServerIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string QuoteMySqlIdentifier(string identifier)
        => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";

    private static string QuoteSourceIdentifier(SourceDatabaseType sourceType, string identifier)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => QuoteSqlServerIdentifier(identifier),
            SourceDatabaseType.MySql => QuoteMySqlIdentifier(identifier),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static string QuoteSqliteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string QuoteSqliteLiteral(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static Options ParseArgs(string[] args)
    {
        var options = new Options();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--source-type":
                    options.SourceType = ParseSourceType(GetRequiredValue(args, ref index, argument));
                    break;
                case "--source-connection-string":
                    options.SourceConnectionString = GetRequiredValue(args, ref index, argument);
                    break;
                case "--target-file":
                    options.TargetFile = GetRequiredValue(args, ref index, argument);
                    break;
                case "--overwrite":
                    options.Overwrite = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    options.ShowHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        return options;
    }

    private static string GetRequiredValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {argumentName}");
        }

        index++;
        return args[index];
    }

    private static SourceDatabaseType ParseSourceType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "mssql" or "sqlserver" or "sql-server" => SourceDatabaseType.SqlServer,
            "mysql" or "mariadb" or "maria" => SourceDatabaseType.MySql,
            _ => throw new ArgumentException($"Unsupported source type: {value}. Expected mssql or mariadb."),
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  Shoko.CLI convert-db [--source-type mssql|mariadb] --source-connection-string \"<connection-string>\" --target-file \"/path/to/Shoko.sqlite\" [--overwrite]");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  - This creates a fresh SQLite database using Shoko's built-in SQLite schema commands.");
        Console.WriteLine("  - Supported source backends: SQL Server and MySQL/MariaDB.");
        Console.WriteLine("  - It copies tables and columns shared by the source schema and target SQLite schema.");
        Console.WriteLine("  - Quartz tables are not included because Quartz uses a separate database configuration.");
    }

    private sealed class Options
    {
        public SourceDatabaseType SourceType { get; set; } = SourceDatabaseType.SqlServer;
        public string SourceConnectionString { get; set; } = string.Empty;
        public string TargetFile { get; set; } = string.Empty;
        public bool Overwrite { get; set; }
        public bool ShowHelp { get; set; }
    }

    private readonly record struct DatabaseVersionInfo(int Version, int Revision, string? Program);

    private sealed class TemporaryShokoHomeScope : IAsyncDisposable
    {
        private readonly string? _previousShokoHome;
        private readonly string _temporaryHomePath;

        public TemporaryShokoHomeScope()
        {
            _previousShokoHome = Environment.GetEnvironmentVariable("SHOKO_HOME");
            _temporaryHomePath = Path.Combine(Path.GetTempPath(), $"shoko-convert-bootstrap-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryHomePath);
            Environment.SetEnvironmentVariable("SHOKO_HOME", _temporaryHomePath);
            ResetApplicationPaths();
        }

        public ValueTask DisposeAsync()
        {
            Utils.ServiceContainer = null;
            Environment.SetEnvironmentVariable("SHOKO_HOME", _previousShokoHome);
            ResetApplicationPaths();
            try
            {
                if (Directory.Exists(_temporaryHomePath))
                {
                    Directory.Delete(_temporaryHomePath, true);
                }
            }
            catch
            {
                // Best-effort cleanup only. Bootstrap isolation matters more than temp dir removal.
            }

            return ValueTask.CompletedTask;
        }

        private static void ResetApplicationPaths()
        {
            SetPrivateStaticField<ApplicationPaths>("_dataPath", null);
            SetPrivateStaticField<ApplicationPaths>("_instance", null);
        }
    }

    private sealed class SqliteColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool NotNull { get; set; }
        public string? DefaultValue { get; set; }
    }

    private static IHost CreateBootstrapHost(SystemService systemService, object settings)
    {
        var initWebHostMethod = typeof(SystemService).GetMethod("InitWebHost", BindingFlags.Instance | BindingFlags.NonPublic)
                                ?? throw new InvalidOperationException("Unable to locate SystemService.InitWebHost.");
        return (IHost)(initWebHostMethod.Invoke(systemService, [settings])
                       ?? throw new InvalidOperationException("SystemService.InitWebHost returned null."));
    }

    private static bool RunInitializeDatabase(SystemService systemService, DatabaseFactory databaseFactory, RepoFactory repositoryFactory)
    {
        var initializeDatabaseMethod = typeof(SystemService).GetMethod("InitializeDatabase", BindingFlags.Instance | BindingFlags.NonPublic)
                                       ?? throw new InvalidOperationException("Unable to locate SystemService.InitializeDatabase.");
        return (bool)(initializeDatabaseMethod.Invoke(systemService, [databaseFactory, repositoryFactory, default(CancellationToken)])
                      ?? throw new InvalidOperationException("SystemService.InitializeDatabase returned null."));
    }

    private static T GetRequiredPrivateField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Unable to locate field {instance.GetType().Name}.{fieldName}.");
        return (T)(field.GetValue(instance) ?? throw new InvalidOperationException($"Field {instance.GetType().Name}.{fieldName} was null."));
    }

    private static void SetPrivateStaticField<TDeclaring>(string fieldName, object? value)
    {
        var field = typeof(TDeclaring).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Unable to locate static field {typeof(TDeclaring).Name}.{fieldName}.");
        field.SetValue(null, value);
    }

    private static void InitializeCorePluginOnly(PluginManager pluginManager, SystemService systemService)
    {
        var pluginTypes = GetRequiredPrivateField<List<LocalPluginInfo>>(pluginManager, "_pluginTypes");
        if (pluginTypes.Count > 0)
        {
            return;
        }

        var coreAssembly = typeof(CorePlugin).Assembly;
        pluginTypes.Add(new()
        {
            ID = typeof(CorePlugin).FullName!.ToUuidV5(),
            Name = "Shoko Core",
            Description = string.Empty,
            Version = systemService.Version,
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            LoadOrder = 0,
            Thumbnail = null,
            InstalledAt = DateTime.MinValue,
            IsEnabled = true,
            IsActive = false,
            CanLoad = true,
            CanUninstall = false,
            Plugin = null,
            PluginType = typeof(CorePlugin),
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = [coreAssembly.Location],
            Types = coreAssembly.GetExportedTypes(),
        });
    }
}
