using System;
using NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Databases;

public class DatabaseFactory
{
    private readonly object _sessionLock = new();
    private ISessionFactory _sessionFactory;
    private IDatabase _instance;

    public ISessionFactory SessionFactory
    {
        get
        {
            lock (_sessionLock)
            {
                return _sessionFactory ??= Instance.CreateSessionFactory();
            }
        }
    }

    public void CloseSessionFactory()
    {
        _sessionFactory?.Dispose();
        _sessionFactory = null;
    }

    public IDatabase Instance
    {
        get
        {
            if (_instance != null) return _instance;

            var settings = Utils.SettingsProvider.GetSettings();
            if (settings.Database.Type.Trim()
                .Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
            {
                _instance = new SQLServer();
            }
            else if (settings.Database.Type.Trim()
                     .Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
            {
                _instance = new SQLite();
            }
            else
            {
                _instance = new MySQL();
            }

            return _instance;
        }
        set => _instance = value;
    }
}
