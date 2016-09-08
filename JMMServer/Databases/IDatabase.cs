using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate;

namespace JMMServer.Databases
{
    public interface IDatabase
    {
        ISessionFactory CreateSessionFactory();
        bool DatabaseAlreadyExists();
        void CreateDatabase();
        bool CreateInitialSchema();
        void UpdateSchema();
        int GetDatabaseVersion();
        void BackupDatabase(string fullfilename);
        ArrayList GetData(string sql);
        string Name { get; }
        int RequiredVersion { get; }
    }
}
