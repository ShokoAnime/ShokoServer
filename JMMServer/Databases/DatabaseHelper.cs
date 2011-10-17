using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using NLog;
using NHibernate;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;

namespace JMMServer.Databases
{
	public class DatabaseHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static ISessionFactory CreateSessionFactory()
		{
			
			if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLSERVER")
			{
				string connectionstring = string.Format(@"data source={0};initial catalog={1};persist security info=True;user id={2};password={3}",
					ServerSettings.DatabaseServer, ServerSettings.DatabaseName, ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword);

				return Fluently.Configure()
				.Database(MsSqlConfiguration.MsSql2008.ConnectionString(connectionstring))
				.Mappings(m =>
					m.FluentMappings.AddFromAssemblyOf<JMMService>())
				.BuildSessionFactory();
			}
			else if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLITE")
			{
				return Fluently.Configure()
				.Database(SQLiteConfiguration.Standard
					.UsingFile(SQLite.GetDatabaseFilePath()))
				.Mappings(m =>
					m.FluentMappings.AddFromAssemblyOf<JMMService>())
				.BuildSessionFactory();
			}
			else
				return null;
		}

		public static bool InitDB()
		{
			try
			{
				if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLSERVER")
				{
					if (!SQLServer.DatabaseAlreadyExists())
					{
						logger.Error("Database: {0} does not exist", ServerSettings.DatabaseName);
						return false;
					}

					SQLServer.CreateInitialSchema();
					SQLServer.UpdateSchema();

					return true;
				}
				else
				{
					SQLite.CreateDatabase();
					SQLite.CreateInitialSchema();
					SQLite.UpdateSchema();
					SQLite.PopulateInitialData();

					return true;
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Could not init database: " + ex.ToString(), ex);
				return false;
			}
		}

		
	}
}
