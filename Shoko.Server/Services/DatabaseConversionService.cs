using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Force.DeepCloner;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Services;

internal sealed class DatabaseConversionOptions
{
    public DatabaseConversionService.SourceDatabaseType SourceType { get; set; } = DatabaseConversionService.SourceDatabaseType.SqlServer;
    public bool SourceTypeProvided { get; set; }
    public string SourceConnectionString { get; set; } = string.Empty;
    public bool SourceConnectionStringProvided { get; set; }
    public string TargetFile { get; set; } = string.Empty;
    public bool TargetFileProvided { get; set; }
    public bool Overwrite { get; set; }
    public bool ShowHelp { get; set; }

    public static bool TryParse(string[] args, [NotNullWhen(true)] out DatabaseConversionOptions? options)
    {
        options = null;
        var conversionModeDetected = DetectConversionMode(args);
        if (!conversionModeDetected)
        {
            return false;
        }

        var parsed = new DatabaseConversionOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--convert-db":
                    break;
                case "--source-type":
                    parsed.SourceType = DatabaseConversionService.ParseSourceType(GetRequiredValue(args, ref index, argument));
                    parsed.SourceTypeProvided = true;
                    break;
                case "--source-connection-string":
                    parsed.SourceConnectionString = GetRequiredValue(args, ref index, argument);
                    parsed.SourceConnectionStringProvided = true;
                    break;
                case "--target-file":
                    parsed.TargetFile = GetRequiredValue(args, ref index, argument);
                    parsed.TargetFileProvided = true;
                    break;
                case "--overwrite":
                    parsed.Overwrite = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    parsed.ShowHelp = true;
                    break;
            }
        }

        options = parsed;
        return true;
    }

    private static bool DetectConversionMode(string[] args)
        => args.Any(argument => string.Equals(argument, "--convert-db", StringComparison.OrdinalIgnoreCase));

    private static string GetRequiredValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {argumentName}");
        }

        index++;
        return args[index];
    }
}

internal static class DatabaseConversionService
{
    private const string TableNameParameter = "@tableName";

    internal enum SourceDatabaseType
    {
        SqlServer,
        MySql,
    }

    internal readonly record struct ResolvedSource(SourceDatabaseType SourceType, string SourceConnectionString);
    internal readonly record struct ResolvedTarget(string TargetFile);
    internal readonly record struct ConversionRuntimeContext(
        DatabaseConversionOptions Options,
        ResolvedSource Source,
        string PreparedTargetFile,
        string TargetConnectionString,
        string TemporaryHomePath,
        ISettingsProvider RuntimeSettingsProvider);

    private static readonly HashSet<string> ExcludedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Versions",
    };

    internal static ConversionRuntimeContext PrepareRuntime(DatabaseConversionOptions options, Shoko.Server.Settings.ServerSettings realSettings)
    {
        if (options.ShowHelp)
        {
            throw new InvalidOperationException("Help output does not require conversion runtime preparation.");
        }

        var resolvedSource = ResolveSource(options, realSettings);
        var resolvedTarget = ResolveTarget(options, realSettings);
        var preparedTargetFile = PrepareTargetPath(resolvedTarget.TargetFile, options.Overwrite);
        var targetConnectionString = BuildSqliteConnectionString(preparedTargetFile);
        var temporaryHomePath = Path.Combine(Path.GetTempPath(), $"shoko-convert-home-{Guid.NewGuid():N}");
        var runtimeSettings = realSettings.DeepClone();
        runtimeSettings.Database.Type = Shoko.Server.Server.Constants.DatabaseType.SQLite;
        runtimeSettings.Database.OverrideConnectionString = targetConnectionString;
        runtimeSettings.Quartz.DatabaseType = Shoko.Server.Server.Constants.DatabaseType.SQLite;
        runtimeSettings.Quartz.ConnectionString = BuildTemporaryQuartzConnectionString(temporaryHomePath);
        return new(options, resolvedSource, preparedTargetFile, targetConnectionString, temporaryHomePath, new InMemorySettingsProvider(runtimeSettings));
    }

    internal static IDisposable BeginIsolatedRuntime(ConversionRuntimeContext context)
        => new ConversionIsolationScope(context);

    internal static async Task RunAsync(SystemService systemService, IServiceProvider services, ConversionRuntimeContext context, CancellationToken cancellationToken)
    {
        await ConvertAsync(systemService, services, context.Source.SourceType, context.Source.SourceConnectionString, context.PreparedTargetFile, cancellationToken);
    }

    internal static ResolvedSource ResolveSource(DatabaseConversionOptions options, Shoko.Server.Settings.IServerSettings settings)
    {
        var configuredSourceType = TryResolveConfiguredSourceType(settings.Database.Type);
        var resolvedSourceType = options.SourceTypeProvided
            ? options.SourceType
            : configuredSourceType ?? throw new InvalidOperationException(
                "The current configured source database is SQLite. Conversion only supports SQL Server/MySQL/MariaDB -> SQLite. " +
                "Provide both --source-type and --source-connection-string to convert from an external supported source.");

        var resolvedConnectionString = options.SourceConnectionStringProvided
            ? options.SourceConnectionString
            : ResolveConfiguredSourceConnectionString(settings.Database, configuredSourceType, resolvedSourceType);

        return new(resolvedSourceType, resolvedConnectionString);
    }

    internal static ResolvedTarget ResolveTarget(DatabaseConversionOptions options, Shoko.Server.Settings.IServerSettings settings)
    {
        if (options.TargetFileProvided)
        {
            if (string.IsNullOrWhiteSpace(options.TargetFile))
            {
                throw new InvalidOperationException(GetUsage());
            }

            return new(Path.GetFullPath(options.TargetFile));
        }

        return new(GetDefaultSqliteTargetPath(settings.Database));
    }

    private static string ResolveConfiguredSourceConnectionString(
        Shoko.Server.Settings.DatabaseSettings settings,
        SourceDatabaseType? configuredSourceType,
        SourceDatabaseType requestedSourceType)
    {
        if (!configuredSourceType.HasValue)
        {
            throw new InvalidOperationException(
                "The current configured source database is SQLite. Conversion only supports SQL Server/MySQL/MariaDB -> SQLite. " +
                "Provide --source-connection-string to override the source details.");
        }

        if (configuredSourceType.Value != requestedSourceType)
        {
            throw new InvalidOperationException(
                $"The current configured source database is {GetSourceTypeDisplayName(configuredSourceType.Value)}, but the requested source type is {GetSourceTypeDisplayName(requestedSourceType)}. " +
                "Provide --source-connection-string when overriding the source type.");
        }

        return BuildConfiguredSourceConnectionString(settings, configuredSourceType.Value);
    }

    private static SourceDatabaseType? TryResolveConfiguredSourceType(Shoko.Server.Server.Constants.DatabaseType configuredType)
    {
        return configuredType switch
        {
            Shoko.Server.Server.Constants.DatabaseType.SQLServer => SourceDatabaseType.SqlServer,
            Shoko.Server.Server.Constants.DatabaseType.MySQL => SourceDatabaseType.MySql,
            Shoko.Server.Server.Constants.DatabaseType.SQLite => null,
            _ => null,
        };
    }

    private static string BuildConfiguredSourceConnectionString(Shoko.Server.Settings.DatabaseSettings settings, SourceDatabaseType sourceType)
    {
        if (!string.IsNullOrWhiteSpace(settings.OverrideConnectionString))
        {
            return settings.OverrideConnectionString;
        }

        return sourceType switch
        {
            SourceDatabaseType.SqlServer =>
                $"data source={settings.Hostname},{settings.Port};Initial Catalog={settings.Schema};user id={settings.Username};password={settings.Password};persist security info=True;MultipleActiveResultSets=True;TrustServerCertificate=True",
            SourceDatabaseType.MySql =>
                $"Server={settings.Hostname};Port={settings.Port};Database={settings.Schema};User ID={settings.Username};Password={settings.Password};Default Command Timeout=3600;Allow User Variables=true",
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static string GetDefaultSqliteTargetPath(Shoko.Server.Settings.DatabaseSettings settings)
    {
        var databaseDirectory = string.IsNullOrWhiteSpace(settings.MySqliteDirectory)
            ? ApplicationPaths.StaticDataPath
            : Path.Combine(ApplicationPaths.StaticDataPath, settings.MySqliteDirectory);
        var databaseFile = string.IsNullOrWhiteSpace(settings.SQLite_DatabaseFile) ? "Shoko.db3" : settings.SQLite_DatabaseFile;
        return Path.GetFullPath(Path.Combine(databaseDirectory, databaseFile));
    }

    private static async Task ConvertAsync(SystemService systemService, IServiceProvider services, SourceDatabaseType sourceType, string sourceConnectionString, string targetFile, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Resolved target SQLite path: {targetFile}");

        await using var source = await OpenSourceConnectionAsync(sourceType, sourceConnectionString);
        await EnsureSourceVersionSupportedAsync(systemService, source, sourceType);

        await InitializeSqliteDatabaseAsync(systemService, services, cancellationToken);

        await using var target = new SqliteConnection(BuildSqliteConnectionString(targetFile));
        await target.OpenAsync();

        await ConfigureSqliteAsync(target);

        var sourceTables = await GetSourceTablesAsync(source, sourceType);
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
        Console.WriteLine($"Conversion completed successfully: {targetFile}");
    }

    internal static string PrepareTargetPath(string targetFile, bool overwrite)
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

        return fullTargetPath;
    }

    private static async Task EnsureSourceVersionSupportedAsync(SystemService systemService, DbConnection source, SourceDatabaseType sourceType)
    {
        var sourceVersion = await GetSourceDatabaseVersionAsync(source, sourceType);
        if (sourceVersion is null)
        {
            throw new InvalidOperationException("The source database does not contain a current Database version entry in Versions. Upgrade it with the matching Shoko Server build before conversion.");
        }

        var expectedVersion = GetExpectedSourceDatabaseVersion(systemService, sourceType);
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

    private static async Task InitializeSqliteDatabaseAsync(SystemService systemService, IServiceProvider services, CancellationToken cancellationToken)
    {
        var databaseFactory = services.GetRequiredService<DatabaseFactory>();
        var repositoryFactory = services.GetRequiredService<RepoFactory>();
        // Conversion mode enters its isolated temp-home/settings scope before host build, so
        // Quartz and any other early services already point at the conversion runtime. Database
        // bootstrap can therefore reuse the existing isolated runtime without touching the real
        // Shoko home or source database settings.
        databaseFactory.CloseSessionFactory();
        databaseFactory.Instance = null;

        if (!systemService.InitializeDatabaseForConversion(databaseFactory, repositoryFactory, cancellationToken))
        {
            throw new InvalidOperationException(systemService.StartupMessage ?? "Shoko database bootstrap failed.");
        }

        databaseFactory.CloseSessionFactory();
        databaseFactory.Instance = null;
    }

    private sealed class InMemorySettingsProvider(Shoko.Server.Settings.ServerSettings settings) : ISettingsProvider
    {
        private Shoko.Server.Settings.ServerSettings _settings = settings;

        public Shoko.Server.Settings.IServerSettings GetSettings(bool copy = false)
            => copy ? _settings.DeepClone() : _settings;

        public void SaveSettings(Shoko.Server.Settings.IServerSettings settings)
        {
            if (settings is Shoko.Server.Settings.ServerSettings serverSettings)
            {
                _settings = serverSettings;
            }
        }

        public void SaveSettings()
        {
        }

        public void DebugSettingsToLog()
        {
        }
    }

    private sealed class ConversionIsolationScope : IDisposable
    {
        private readonly ISettingsProvider _previousSettingsProvider;
        private readonly IDisposable _dataPathOverride;
        private readonly string _temporaryHomePath;
        private bool _disposed;

        public ConversionIsolationScope(ConversionRuntimeContext context)
        {
            _temporaryHomePath = context.TemporaryHomePath;
            Directory.CreateDirectory(_temporaryHomePath);

            _previousSettingsProvider = ISettingsProvider.Instance;
            ISettingsProvider.Instance = context.RuntimeSettingsProvider;

            _dataPathOverride = ApplicationPaths.PushDataPathOverride(_temporaryHomePath);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dataPathOverride.Dispose();
            ISettingsProvider.Instance = _previousSettingsProvider;

            try
            {
                if (Directory.Exists(_temporaryHomePath))
                {
                    Directory.Delete(_temporaryHomePath, true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }

            _disposed = true;
        }
    }

    private static string BuildTemporaryQuartzConnectionString(string temporaryHomePath)
        => $"Data Source={Path.Combine(temporaryHomePath, "SQLite", "Quartz.db3")};Mode=ReadWriteCreate;Pooling=True";

    private static string BuildSqliteConnectionString(string targetFile)
        => new SqliteConnectionStringBuilder
        {
            DataSource = targetFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

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
            await reader.IsDBNullAsync(2) ? null : reader.GetString(2));
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
            await reader.IsDBNullAsync(2) ? null : reader.GetString(2));
    }

    private static DatabaseVersionInfo GetExpectedSourceDatabaseVersion(SystemService systemService, SourceDatabaseType sourceType)
    {
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

    [SuppressMessage("Major Code Smell", "S3011", Justification = "The converter inspects internal database command containers to determine the expected backend migration version without duplicating migration metadata.")]
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
        var sourceColumnTypes = await GetSourceColumnTypesAsync(source, sourceType, tableName);
        var targetColumns = await GetSqliteColumnsAsync(target, tableName);
        var sourceColumnSet = sourceColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceBackedColumns = targetColumns.Where(column => sourceColumnSet.Contains(column.Name)).ToList();
        var guidColumns = sourceBackedColumns
            .Where(column => IsGuidColumn(sourceType, sourceColumnTypes, column))
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

        await using var transaction = (SqliteTransaction)await target.BeginTransactionAsync();
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
                insertCommand.Parameters[index].Value = NormalizeValue(value, guidColumns.Contains(sourceBackedColumns[index].Name));
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
        var sourceColumnTypes = await GetSourceColumnTypesAsync(source, sourceType, tableName);
        var targetColumns = await GetSqliteColumnsAsync(target, tableName);
        var targetColumnLookup = targetColumns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
        var guidColumns = sharedColumns
            .Where(column => targetColumnLookup.TryGetValue(column, out var targetColumn) && IsGuidColumn(sourceType, sourceColumnTypes, targetColumn))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderColumns = await ResolveOrderColumnsAsync(source, sourceType, target, tableName, sharedColumns, sourceColumnTypes);

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
            var sourceRow = await ReadNormalizedRowAsync(sourceReader, sharedColumns, guidColumns);
            var targetRow = await ReadNormalizedRowAsync(targetReader, sharedColumns, guidColumns);
            ThrowIfRowMismatch(tableName, sharedColumns, orderColumns, rowNumber, sourceRow, targetRow);
        }
    }

    private static async Task<List<string>> ResolveOrderColumnsAsync(
        DbConnection source,
        SourceDatabaseType sourceType,
        SqliteConnection target,
        string tableName,
        IReadOnlyList<string> sharedColumns,
        IReadOnlyDictionary<string, string> sourceColumnTypes)
    {
        var sourcePrimaryKeys = await GetSourcePrimaryKeyColumnsAsync(source, sourceType, tableName);
        var targetPrimaryKeys = await GetSqlitePrimaryKeyColumnsAsync(target, tableName);
        var orderColumns = sourcePrimaryKeys
            .Where(column => targetPrimaryKeys.Contains(column, StringComparer.OrdinalIgnoreCase))
            .Where(column => sharedColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (orderColumns.Count > 0)
        {
            return orderColumns;
        }

        orderColumns = sharedColumns
            .Where(column => sourceColumnTypes.TryGetValue(column, out var type) && IsSourceOrderableType(sourceType, type))
            .ToList();

        if (orderColumns.Count > 0)
        {
            return orderColumns;
        }

        throw new InvalidOperationException($"Verification failed for {tableName}: no primary key or SQL-orderable shared columns are available for deterministic comparison.");
    }

    private static async Task<string[]> ReadNormalizedRowAsync(DbDataReader reader, IReadOnlyList<string> columnNames, IReadOnlySet<string> guidColumns)
    {
        var row = new string[columnNames.Count];
        for (var index = 0; index < columnNames.Count; index++)
        {
            row[index] = NormalizeComparisonValue(await reader.IsDBNullAsync(index) ? null : reader.GetValue(index), guidColumns.Contains(columnNames[index]));
        }

        return row;
    }

    private static void ThrowIfRowMismatch(
        string tableName,
        IReadOnlyList<string> sharedColumns,
        IReadOnlyList<string> orderColumns,
        long rowNumber,
        IReadOnlyList<string> sourceRow,
        IReadOnlyList<string> targetRow)
    {
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
                var keyIndex = -1;
                for (var sharedColumnIndex = 0; sharedColumnIndex < sharedColumns.Count; sharedColumnIndex++)
                {
                    if (!string.Equals(sharedColumns[sharedColumnIndex], column, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    keyIndex = sharedColumnIndex;
                    break;
                }

                var keyValue = keyIndex >= 0 ? sourceRow[keyIndex] : "<missing>";
                return $"{column}={keyValue}";
            }));
            throw new InvalidOperationException($"Verification failed for {tableName}: column {sharedColumns[index]} mismatch at row {rowNumber} ({keyDescription}). Source={sourceValue}, Target={targetValue}.");
        }
    }

    private static object NormalizeValue(object value, bool isGuidColumn)
    {
        return value switch
        {
            DBNull => DBNull.Value,
            Guid guid => NormalizeGuid(guid),
            string text when isGuidColumn && TryNormalizeGuidString(text, out var normalizedGuidText) => normalizedGuidText,
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

    private static bool TryNormalizeGuidString(string value, out string normalized)
    {
        if (Guid.TryParse(value, out var guid))
        {
            normalized = NormalizeGuid(guid);
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static string NormalizeGuid(Guid guid)
    {
        return guid.ToString("D").ToUpperInvariant();
    }

    private static bool IsGuidColumn(SourceDatabaseType sourceType, IReadOnlyDictionary<string, string> sourceColumnTypes, SqliteColumnInfo targetColumn)
    {
        if (IsGuidType(targetColumn.Type))
        {
            return true;
        }

        return sourceColumnTypes.TryGetValue(targetColumn.Name, out var sourceTypeName) && IsGuidType(sourceType, sourceTypeName);
    }

    private static bool IsGuidType(SourceDatabaseType sourceType, string typeName)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => typeName.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase),
            SourceDatabaseType.MySql => false,
            _ => false,
        };
    }

    private static bool IsGuidType(string typeName)
        => typeName.Contains("UNIQUEIDENTIFIER", StringComparison.OrdinalIgnoreCase);

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
        var sql = $"""
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = {TableNameParameter}
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue(TableNameParameter, tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<string>> GetMySqlColumnsAsync(MySqlConnection connection, string tableName)
    {
        var sql = $"""
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = {TableNameParameter}
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue(TableNameParameter, tableName);
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
        var sql = $"""
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
              AND TABLE_NAME = {TableNameParameter}
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue(TableNameParameter, tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<string>> GetMySqlPrimaryKeyColumnsAsync(MySqlConnection connection, string tableName)
    {
        var sql = $"""
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = {TableNameParameter}
              AND CONSTRAINT_NAME = 'PRIMARY'
            ORDER BY ORDINAL_POSITION
            """;

        var columns = new List<string>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue(TableNameParameter, tableName);
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
        var sql = $"""
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = {TableNameParameter}
            """;

        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue(TableNameParameter, tableName);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        return columns;
    }

    private static async Task<Dictionary<string, string>> GetMySqlColumnTypesAsync(MySqlConnection connection, string tableName)
    {
        var sql = $"""
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = {TableNameParameter}
            """;

        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue(TableNameParameter, tableName);
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
                Type = await reader.IsDBNullAsync(2) ? string.Empty : reader.GetString(2),
                NotNull = reader.GetInt32(3) != 0,
                DefaultValue = await reader.IsDBNullAsync(4) ? null : reader.GetString(4),
            });
        }

        return columns;
    }

    private static string BuildSourceOrderedSelectSql(SourceDatabaseType sourceType, string tableName, IReadOnlyList<string> selectColumns, IReadOnlyList<string> orderColumns, IReadOnlyDictionary<string, string> columnTypes)
    {
        return sourceType switch
        {
            SourceDatabaseType.SqlServer => BuildSqlServerOrderedSelectSql(tableName, selectColumns, orderColumns, columnTypes),
            SourceDatabaseType.MySql => BuildMySqlOrderedSelectSql(tableName, selectColumns, orderColumns),
            _ => throw new InvalidOperationException($"Unsupported source database type: {sourceType}"),
        };
    }

    private static string BuildSqlServerOrderedSelectSql(string tableName, IReadOnlyList<string> selectColumns, IReadOnlyList<string> orderColumns, IReadOnlyDictionary<string, string> columnTypes)
    {
        var selectList = string.Join(", ", selectColumns.Select(column => BuildSqlServerSelectExpression(column, columnTypes)));
        var orderList = string.Join(", ", orderColumns.Select(column => BuildSqlServerOrderExpression(column, columnTypes)));
        return $"SELECT {selectList} FROM {QuoteSqlServerIdentifier(tableName)} ORDER BY {orderList};";
    }

    private static string BuildMySqlOrderedSelectSql(string tableName, IReadOnlyList<string> selectColumns, IReadOnlyList<string> orderColumns)
    {
        var selectList = string.Join(", ", selectColumns.Select(BuildMySqlSelectExpression));
        var orderList = string.Join(", ", orderColumns.Select(BuildMySqlOrderExpression));
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

    private static string BuildMySqlSelectExpression(string columnName)
    {
        return QuoteMySqlIdentifier(columnName);
    }

    private static string BuildMySqlOrderExpression(string columnName)
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

    private static string NormalizeComparisonValue(object? value, bool isGuidColumn)
    {
        return value switch
        {
            null or DBNull => "<null>",
            byte[] bytes => Convert.ToHexString(bytes),
            Guid guid => NormalizeGuid(guid),
            string text when isGuidColumn && TryNormalizeGuidString(text, out var normalizedGuidText) => normalizedGuidText,
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

    private static string ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("SQL identifier cannot be null or empty.");
        }

        if (!(char.IsLetter(identifier[0]) || identifier[0] == '_'))
        {
            throw new InvalidOperationException($"Unsupported SQL identifier: {identifier}");
        }

        for (var index = 1; index < identifier.Length; index++)
        {
            var character = identifier[index];
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                continue;
            }

            throw new InvalidOperationException($"Unsupported SQL identifier: {identifier}");
        }

        return identifier;
    }

    private static string QuoteSqlServerIdentifier(string identifier)
        => $"[{ValidateIdentifier(identifier).Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string QuoteMySqlIdentifier(string identifier)
        => $"`{ValidateIdentifier(identifier).Replace("`", "``", StringComparison.Ordinal)}`";

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
        => $"\"{ValidateIdentifier(identifier).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string QuoteSqliteLiteral(string value)
        => $"'{ValidateIdentifier(value).Replace("'", "''", StringComparison.Ordinal)}'";

    internal static SourceDatabaseType ParseSourceType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "mssql" or "sqlserver" or "sql-server" => SourceDatabaseType.SqlServer,
            "mysql" or "mariadb" or "maria" => SourceDatabaseType.MySql,
            _ => throw new ArgumentException($"Unsupported source type: {value}. Expected mssql or mariadb."),
        };
    }

    internal static string GetUsage()
        => """
           Usage:
             ShokoServer --convert-db [--source-type mssql|mariadb] [--source-connection-string "<connection-string>"] [--target-file "/path/to/Shoko.sqlite"] [--overwrite]

           Notes:
             - If omitted, source type and connection details default to the current ServerSettings.Database configuration.
             - If omitted, the target SQLite file defaults to Shoko's normal SQLite database path under the current Shoko home/data directory.
             - Explicit --source-type and/or --source-connection-string override the configured source details.
             - Explicit --target-file overrides the default SQLite target path.
             - The resolved source database must be SQL Server or MySQL/MariaDB. SQLite cannot be used as a source.
             - This creates a fresh SQLite database using Shoko's built-in SQLite schema commands.
             - Supported source backends: SQL Server and MySQL/MariaDB.
             - It copies tables and columns shared by the source schema and target SQLite schema.
             - Quartz tables are not included because Quartz uses a separate database configuration.
           """;

    private readonly record struct DatabaseVersionInfo(int Version, int Revision, string? Program);

    private sealed class SqliteColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool NotNull { get; set; }
        public string? DefaultValue { get; set; }
    }
}
