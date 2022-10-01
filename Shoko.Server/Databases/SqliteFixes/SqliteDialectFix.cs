using System.Data.Common;
using NHibernate.Dialect;
using NHibernate.Dialect.Schema;

namespace Shoko.Server.Databases.SqliteFixes;

public class SqliteDialectFix : SQLiteDialect
{
    public override IDataBaseSchema GetDataBaseSchema(DbConnection connection)
    {
        return new SqliteMetadataFix(connection, this);
    }
}
