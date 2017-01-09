using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NHibernate;
using NLog;
using Shoko.Server.Repositories;

namespace Shoko.Server.Databases
{
    public static class DatabaseFactory
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        private static readonly object sessionLock = new object();

        private static ISessionFactory sessionFactory = null;

        public static ISessionFactory SessionFactory
        {
            get
            {
                lock (sessionLock)
                {
                    if (sessionFactory == null)
                    {
                        //logger.Info("Creating new session...");
                        sessionFactory = Instance.CreateSessionFactory();
                    }
                    return sessionFactory;
                }
            }
        }

        public static void CloseSessionFactory()
        {
            if (sessionFactory != null) sessionFactory.Dispose();
            sessionFactory = null;
        }

        private static IDatabase _instance;
        public static IDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (ServerSettings.DatabaseType.Trim()
                        .Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
                        _instance = new SQLServer();
                    else if (ServerSettings.DatabaseType.Trim()
                        .Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
                        _instance = new SQLite();
                    else
                        _instance = new MySQL();
                }
                return _instance;
            }
            set { _instance = value; }
        }

        public static bool InitDB()
        {
            try
            {
                Instance = null;
                if (!Instance.DatabaseAlreadyExists())
                {
                    Instance.CreateDatabase();
                    Thread.Sleep(3000);
                }
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                DatabaseFactory.CloseSessionFactory();
                ServerState.Instance.CurrentSetupStatus = Shoko.Server.Properties.Resources.Database_Initializing;
                ISessionFactory temp = DatabaseFactory.SessionFactory;
                Instance.Init();
                int version = Instance.GetDatabaseVersion();
                if (version > Instance.RequiredVersion)
                {
                    ServerState.Instance.CurrentSetupStatus =
                        Shoko.Server.Properties.Resources.Database_NotSupportedVersion;
                    return false;
                }
                if (version!=0 && version < Instance.RequiredVersion)
                {
                    ServerState.Instance.CurrentSetupStatus = Shoko.Server.Properties.Resources.Database_Backup;
                    Instance.BackupDatabase(Instance.GetDatabaseBackupName(version));
                }
                try
                {
                    Instance.CreateAndUpdateSchema();
                    RepoFactory.Init();
                    Instance.ExecuteDatabaseFixes();
                    Instance.PopulateInitialData();
                }
                catch (Exception ex)
                {
                    if (ex is DatabaseCommandException)
                    {
                        logger.Error(ex, ex.ToString());
                        MessageBox.Show(
                            "Database Error :\n\r " + ex.ToString() +
                            "\n\rNotify developers about this error, it will be logged in your logs", "Database Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        ServerState.Instance.CurrentSetupStatus = Shoko.Server.Properties.Resources.Server_DatabaseFail;
                    }
                    else
                    {
                        if (ex is TimeoutException)
                        {
                            logger.Error(ex, "Database TimeOut: " + ex.ToString());
                            ServerState.Instance.CurrentSetupStatus = Shoko.Server.Properties.Resources.Server_DatabaseTimeOut;
                        }
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Could not init database: " + ex.ToString());
                ServerState.Instance.CurrentSetupStatus = Shoko.Server.Properties.Resources.Server_DatabaseFail;
                return false;
            }
        }
    }
}
