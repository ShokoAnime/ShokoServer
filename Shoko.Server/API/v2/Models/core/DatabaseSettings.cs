using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.core
{
    public class DatabaseSettings
    {
        // case invariant, no spaces representation of db type
        public DatabaseTypes? db_type { get; set; }

        public string sqlserver_databaseserver { get; set; }

        public string sqlserver_databasename { get; set; }

        public string sqlserver_username { get; set; }

        public string sqlserver_password { get; set; }

        public string sqlite_databasefile { get; set; }

        public string mysql_hostname { get; set; }

        public string mysql_schemaname { get; set; }

        public string mysql_username { get; set; }

        public string mysql_password { get; set; }
    }
}