using NHibernate;

namespace Shoko.Server.Databases;

public interface IDatabase
{
    ISessionFactory CreateSessionFactory();
    bool DatabaseAlreadyExists();
    void CreateDatabase();
    void CreateAndUpdateSchema();
    void BackupDatabase(string fullfilename);
    string Name { get; }
    int RequiredVersion { get; }
    string GetDatabaseBackupName(int version);
    void ExecuteDatabaseFixes();
    void PopulateInitialData();
    int GetDatabaseVersion();
    void Init();
    bool HasVersionsTable();
    public string GetTestConnectionString();
    public string GetConnectionString();
    bool TestConnection();
}
