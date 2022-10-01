using System.Data.Common;
using NHibernate.Dialect;
using NHibernate.Dialect.Schema;

namespace Shoko.Server.Databases.SqliteFixes;

public class SqliteMetadataFix : SQLiteDataBaseMetaData
{
    public SqliteMetadataFix(DbConnection connection, Dialect dialect) : base(connection, dialect)
    {
    }

    public override bool IncludeDataTypesInReservedWords => false;
}
