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
    void Vacuum();

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
