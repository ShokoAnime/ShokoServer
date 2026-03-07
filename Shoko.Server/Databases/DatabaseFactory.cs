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
            return _instance = settings.Database.Type switch
            {
                Constants.DatabaseType.SQLServer => new SQLServer(),
                Constants.DatabaseType.MySQL => new MySQL(),
                Constants.DatabaseType.PostgreSQL => new PostgreSQL(),
                _ => new SQLite()
            };
        }
        set => _instance = value;
    }
}
