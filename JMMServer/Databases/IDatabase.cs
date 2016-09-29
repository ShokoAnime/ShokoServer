using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMServer.Entities;
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
        Tuple<bool, string> ExecuteCommand(DatabaseCommand cmd);
        Dictionary<string, Dictionary<string, Versions>> AllVersions { get; }
    }
}
