﻿using System;
using System.Globalization;
using System.Threading;
using NHibernate;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Databases;

public static class DatabaseFactory
{
    private static Logger logger = LogManager.GetCurrentClassLogger();


    private static readonly object sessionLock = new();

    private static ISessionFactory sessionFactory;

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
        sessionFactory?.Dispose();
        sessionFactory = null;
    }

    private static IDatabase _instance;

    public static IDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                if (ServerSettings.Instance.Database.Type.Trim()
                    .Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
                {
                    _instance = new SQLServer();
                }
                else if (ServerSettings.Instance.Database.Type.Trim()
                         .Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
                {
                    _instance = new SQLite();
                }
                else
                {
                    _instance = new MySQL();
                }
            }

            return _instance;
        }
        set => _instance = value;
    }

    public static bool InitDB(out string errorMessage)
    {
        try
        {
            _instance = null;
            for (var i = 0; i < 60; i++)
            {
                if (Instance.TestConnection())
                {
                    logger.Info("Database Connection OK!");
                    break;
                }

                if (i == 59)
                {
                    logger.Error(errorMessage = "Unable to connect to database!");
                    return false;
                }

                logger.Info("Waiting for database connection...");
                Thread.Sleep(1000);
            }

            if (!Instance.DatabaseAlreadyExists())
            {
                Instance.CreateDatabase();
                Thread.Sleep(3000);
            }

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

            CloseSessionFactory();

            var message = Resources.Database_Initializing;
            logger.Info($"Starting Server: {message}");
            ServerState.Instance.ServerStartingStatus = message;

            Instance.Init();
            var version = Instance.GetDatabaseVersion();
            if (version > Instance.RequiredVersion)
            {
                message = Resources.Database_NotSupportedVersion;
                logger.Info($"Starting Server: {message}");
                ServerState.Instance.ServerStartingStatus = message;
                errorMessage = Resources.Database_NotSupportedVersion;
                return false;
            }

            if (version != 0 && version < Instance.RequiredVersion)
            {
                message = Resources.Database_Backup;
                logger.Info($"Starting Server: {message}");
                ServerState.Instance.ServerStartingStatus = message;
                Instance.BackupDatabase(Instance.GetDatabaseBackupName(version));
            }

            try
            {
                logger.Info($"Starting Server: {Instance.GetType()} - CreateAndUpdateSchema()");
                Instance.CreateAndUpdateSchema();

                logger.Info("Starting Server: RepoFactory.Init()");
                RepoFactory.Init();
                Instance.ExecuteDatabaseFixes();
                Instance.PopulateInitialData();
                RepoFactory.PostInit();
            }
            catch (DatabaseCommandException ex)
            {
                logger.Error(ex, ex.ToString());
                Utils.ShowErrorMessage("Database Error :\n\r " + ex +
                                       "\n\rNotify developers about this error, it will be logged in your logs",
                    "Database Error");
                ServerState.Instance.ServerStartingStatus = Resources.Server_DatabaseFail;
                errorMessage = "Database Error :\n\r " + ex +
                               "\n\rNotify developers about this error, it will be logged in your logs";
                return false;
            }
            catch (TimeoutException ex)
            {
                logger.Error(ex, $"Database Timeout: {ex}");
                ServerState.Instance.ServerStartingStatus = Resources.Server_DatabaseTimeOut;
                errorMessage = Resources.Server_DatabaseTimeOut + "\n\r" + ex;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Could not init database: {ex}";
            logger.Error(ex, errorMessage);
            ServerState.Instance.ServerStartingStatus = Resources.Server_DatabaseFail;
            return false;
        }
    }
}
