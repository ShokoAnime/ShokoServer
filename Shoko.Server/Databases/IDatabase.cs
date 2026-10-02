using System;
using System.Threading;
using NHibernate;

namespace Shoko.Server.Databases;

public interface IDatabase
{
    ISessionFactory CreateSessionFactory();
    bool DatabaseAlreadyExists();
    void CreateDatabase();
    void CreateAndUpdateSchema();
    void BackupDatabase(string fullfilename);

    /// <summary>
    /// Gives back the space deleted rows left behind: SQLite rebuilds its
    /// file, MySQL and MariaDB optimize every table, and SQL Server, which
    /// reuses the space itself, does nothing.
    /// </summary>
    /// <remarks>
    /// SQLite's rebuild is one statement, so it reports no progress and runs
    /// to the end once begun; MySQL reports and stops between two tables.
    /// </remarks>
    /// <param name="progress">Told how far the vacuum is, from 0 to 100.</param>
    /// <param name="token">Stops the vacuum before it begins, or between two tables.</param>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    void Vacuum(IProgress<decimal>? progress = null, CancellationToken token = default);

    string Name { get; }
    int RequiredVersion { get; }
    string GetDatabaseBackupName(int version);
    void ExecuteDatabaseFixes();
    void PopulateInitialData();
    int GetDatabaseVersion();

    /// <summary>
    /// Whether any step of the schema, a data fix included, has not been
    /// applied to the database yet.
    /// </summary>
    /// <returns><c>true</c> when the next update will change the database.</returns>
    bool HasPendingSchemaSteps();

    void Init();
    bool HasVersionsTable();
    public string GetTestConnectionString();
    public string GetConnectionString();
    bool TestConnection();
}
