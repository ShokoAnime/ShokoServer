using System;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;

namespace Shoko.Server.API.v2.Modules
{
    public class Database : Nancy.NancyModule
    {
        public static int version = 1;

        public Database() : base("/api/db")
        {
            if (!ServerSettings.FirstRun)
            {
                this.RequiresAuthentication();
            }

            Post["/set", true] = async (x,ct) => await Task.Factory.StartNew(SetupDB, ct);
            Get["/get", true] = async (x,ct) => await Task.Factory.StartNew(GetDB, ct);
            Get["/start", true] = async (x,ct) => await Task.Factory.StartNew(RunDB, ct);
            Get["/check", true] = async (x,ct) => await Task.Factory.StartNew(CheckDB, ct);
        }

        #region Setup

        /// <summary>
        /// Setup Database and Init it
        /// </summary>
        /// <returns></returns>
        private object SetupDB()
        {
            Models.core.Database db = this.Bind();
            if (!String.IsNullOrEmpty(db.type) && db.type != "")
            {
                switch (db.type.ToLower())
                {
                    case "sqlite":
                        ServerSettings.DatabaseType = "SQLite";
                        ServerSettings.DatabaseFile = db.path;
                        break;

                    case "sqlserver":
                        ServerSettings.DatabaseType = "SQLServer";
                        ServerSettings.DatabaseUsername = db.login;
                        ServerSettings.DatabasePassword = db.password;
                        ServerSettings.DatabaseName = db.table;
                        ServerSettings.DatabaseServer = db.server;
                        break;

                    case "mysql":
                        ServerSettings.DatabaseType = "MySQL";
                        ServerSettings.MySQL_Username = db.login;
                        ServerSettings.MySQL_Password = db.password;
                        ServerSettings.MySQL_SchemaName = db.table;
                        ServerSettings.MySQL_Hostname = db.server;
                        break;
                }

                //ShokoServer.workerSetupDB.RunWorkerAsync();
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Return Database object
        /// </summary>
        /// <returns></returns>
        private object GetDB()
        {
            Models.core.Database db = new Models.core.Database
            {
                type = ServerSettings.DatabaseType
            };
            if (!String.IsNullOrEmpty(db.type) && db.type != "")
            {
                switch (db.type.ToLower())
                {
                    case "sqlite":
                        db.path = ServerSettings.DatabaseFile;
                        break;

                    case "sqlserver":
                        db.login = ServerSettings.DatabaseUsername;
                        db.password = ServerSettings.DatabasePassword;
                        db.table = ServerSettings.DatabaseName;
                        db.server = ServerSettings.DatabaseServer;
                        break;

                    case "mysql":
                        db.login = ServerSettings.MySQL_Username;
                        db.password = ServerSettings.MySQL_Password;
                        db.table = ServerSettings.MySQL_SchemaName;
                        db.server = ServerSettings.MySQL_Hostname;
                        break;
                }

                return db;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Test and run database
        /// </summary>
        /// <returns></returns>
        private object RunDB()
        {
            try
            {
                if (ServerState.Instance.DatabaseIsSQLite)
                {
                    ServerSettings.DatabaseType = "SQLite";
                }
                else if (ServerState.Instance.DatabaseIsSQLServer)
                {
                    if (string.IsNullOrEmpty(ServerSettings.DatabaseName) ||
                        string.IsNullOrEmpty(ServerSettings.DatabasePassword)
                        || string.IsNullOrEmpty(ServerSettings.DatabaseServer) ||
                        string.IsNullOrEmpty(ServerSettings.DatabaseUsername))
                    {
                        return HttpStatusCode.BadRequest;
                    }
                }
                else if (ServerState.Instance.DatabaseIsMySQL)
                {
                    if (string.IsNullOrEmpty(ServerSettings.MySQL_SchemaName) ||
                        string.IsNullOrEmpty(ServerSettings.MySQL_Password)
                        || string.IsNullOrEmpty(ServerSettings.MySQL_Hostname) ||
                        string.IsNullOrEmpty(ServerSettings.MySQL_Username))
                    {
                        return HttpStatusCode.BadRequest;
                    }
                }

                ShokoServer.workerSetupDB.RunWorkerAsync();
                return HttpStatusCode.OK;
            }
            catch
            {
                return HttpStatusCode.InternalServerError;
            }
        }

        /// <summary>
        /// check if database is valid
        /// </summary>
        /// <returns></returns>
        private object CheckDB()
        {
            if (!ShokoServer.workerSetupDB.IsBusy)
            {
                if (ServerState.Instance.ServerOnline)
                {
                    ServerSettings.FirstRun = false;
                    return "{\"db\": 1}";
                }
                else
                {
                    ServerSettings.FirstRun = true;
                    return "{\"db\": 0}";
                }
            }
            else
            {
                ServerSettings.FirstRun = true;
                return "{\"db\": -1}";
            }
        }

        #endregion
    }
}