using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;
using NHibernate;

namespace JMMServer.Databases
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

    }
}
