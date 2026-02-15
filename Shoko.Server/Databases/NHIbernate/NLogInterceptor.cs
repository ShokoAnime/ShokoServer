using NHibernate;
using NHibernate.SqlCommand;
using NLog;

namespace Shoko.Server.Databases.NHibernate;

public class NLogInterceptor : EmptyInterceptor
{
    private Logger _logger = LogManager.GetCurrentClassLogger();
    public override SqlString OnPrepareStatement(SqlString sql)
    {
        _logger.Trace($"Executing Query: {sql}");

        return base.OnPrepareStatement(sql);
    }
}
