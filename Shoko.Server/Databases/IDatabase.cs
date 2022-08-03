using System.Collections;
using NHibernate;

namespace Shoko.Server.Databases
{
    public interface IDatabase
    {
        ISessionFactory CreateSessionFactory();
        bool DatabaseAlreadyExists();
        void CreateDatabase();
        void CreateAndUpdateSchema();
        void BackupDatabase(string fullfilename);
        ArrayList GetData(string sql);
        string Name { get; }
        int RequiredVersion { get; }
        string GetDatabaseBackupName(int version);
        void ExecuteDatabaseFixes();
        void PopulateInitialData();
        int GetDatabaseVersion();
        void Init();
        bool HasVersionsTable();
        bool TestConnection();
    }
}