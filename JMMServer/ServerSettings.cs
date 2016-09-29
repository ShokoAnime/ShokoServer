using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.RightsManagement;
using System.Threading;
using System.Windows;
using AniDBAPI;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using JMMServer.UI;
using NLog;
using Newtonsoft.Json;
using NLog.Targets;

namespace JMMServer
{
    public static class ServerSettings
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static Dictionary<string, string> appSettings = new Dictionary<string, string>();
        private static bool migrationError = false;
        private static bool migrationActive = false;
        private static MigrationForm migrationForm;


        private static string Get(string key)
        {
            if (appSettings.ContainsKey(key))
                return appSettings[key];
            return null;
        }

        private static void Set(string key, string value)
        {
            string orig = Get(key);
            if (value != orig)
            {
                appSettings[key] = value;
                SaveSettings();
            }
        }


       
        //in this way, we could host two JMMServers int the same machine

        public static string DefaultInstance { get; set; } = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

        public static string ApplicationPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),DefaultInstance);

        public static string DefaultImagePath => Path.Combine(ApplicationPath, "images");

        public static void LoadSettings()
        {
            try
            {
                //Reconfigure log file to applicationpath
                var target = (FileTarget)LogManager.Configuration.FindTargetByName("file");
                target.FileName = ApplicationPath + "/logs/${shortdate}.txt";
                LogManager.ReconfigExistingLoggers();


                disabledSave = true;

                string programlocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                List<MigrationDirectory> migrationdirs = new List<MigrationDirectory>
                {
                    new MigrationDirectory
                    {
                        From = Path.Combine(programlocation, "SQLite"), To = MySqliteDirectory
                    },
                    new MigrationDirectory
                    {
                        From = Path.Combine(programlocation, "DatabaseBackup"), To = DatabaseBackupDirectory
                    },
                    new MigrationDirectory
                    {
                        From = Path.Combine(programlocation, "MyList"), To = MyListDirectory
                    },
                    new MigrationDirectory
                    {
                        From = Path.Combine(programlocation, "Anime_HTTP"), To = AnimeXmlDirectory
                    },
                    new MigrationDirectory
                    {
                        From = Path.Combine(programlocation, "logs"), To = Path.Combine(ApplicationPath,"logs")
                    },
                };
                string path = Path.Combine(ApplicationPath, "settings.json");
                if (File.Exists(path))
                    appSettings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
                else
                {
                    NameValueCollection col = ConfigurationManager.AppSettings;
                    appSettings = col.AllKeys.ToDictionary(a => a, a => col[a]);
                }
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);
                if (BaseImagesPathIsDefault || !Directory.Exists(BaseImagesPath))
                {
                    migrationdirs.Add(new MigrationDirectory
                    {
                        From = Path.Combine(programlocation, "images"),
                        To = DefaultImagePath
                    });
                }
                else if (Directory.Exists(BaseImagesPath))
                {
                    ImagesPath = BaseImagesPath;
                }
                bool migrate = !Directory.Exists(ApplicationPath);
                foreach (MigrationDirectory m in migrationdirs)
                {
                    if (m.ShouldMigrate)
                    {
                        migrate = true;
                        break;
                    }
                }
                if (migrate)
                {
                    migrationActive = true;
                    if (!Utils.IsAdministrator())
                    {
                        MessageBox.Show(Properties.Resources.Migration_AdminFail, Properties.Resources.Migration_Header,
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        Application.Current.Shutdown();
                        return;
                    }


                    Migration m = null;
                    try
                    {
                        /*
                        m =
                            new Migration(
                                $"{Properties.Resources.Migration_AdminPass1} {ApplicationPath}, {Properties.Resources.Migration_AdminPass2}");
                        m.Show();*/

                        // Show migration indicator
                        MigrationIndicatorForm();

                        if (!Directory.Exists(ApplicationPath))
                        {
                            Directory.CreateDirectory(ApplicationPath);
                        }
                        Utils.GrantAccess(ApplicationPath);
                        disabledSave = false;
                        SaveSettings();
                        foreach (MigrationDirectory md in migrationdirs)
                        {
                            if (!md.SafeMigrate())
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(Properties.Resources.Migration_SettingsError + " ", e.ToString());
                        migrationActive = false;
                        migrationError = true;

                    }
                    Utils.SetNetworkRequirements(JMMServerPort, JMMServerFilePort, JMMServerPort, JMMServerFilePort);

                    //m?.Close();

                    migrationActive = false;
                    // We make sure to restart app upcong successfull completion
                    if (!migrationError)
                    {
                        WaitForMigrationThenRestart();

                        // Sleep a bit to allow for slow startup
                        Thread.Sleep(2500);
                    }

                    Application.Current.Shutdown();
                    return;
                }
                disabledSave = false;

                if (Utils.IsAdministrator())
                    Utils.SetNetworkRequirements(JMMServerPort, JMMServerFilePort, JMMServerPort, JMMServerFilePort);
                if (Directory.Exists(BaseImagesPath) && string.IsNullOrEmpty(ImagesPath))
                {
                    ImagesPath = BaseImagesPath;
                }
                if (string.IsNullOrEmpty(ImagesPath))
                    ImagesPath = DefaultImagePath;
                SaveSettings();


            }
            catch (Exception e)
            {
                migrationError = true;
                migrationActive = false;
                MessageBox.Show(Properties.Resources.Migration_LoadError + " ", e.ToString());
                Application.Current.Shutdown();
                return;
            }           
        }

        private static void MigrationIndicatorForm()
        {
            // Configure a BackgroundWorker to perform your long running operation.
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += new DoWorkEventHandler(bg_migrationStart);
            bg.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bg_migrationFinished);

            // Start the worker.
            bg.RunWorkerAsync();

            // Display the migration form.
            migrationForm = new MigrationForm();
            migrationForm.Show();
        }

        private static void bg_migrationStart(object sender, DoWorkEventArgs e)
        {
            while (migrationActive && !migrationError) { };
        }
        private static void bg_migrationFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            // Retrieve the result pass from bg_DoWork() if any.
            // Note, you may need to cast it to the desired data type.
            //object result = e.Result;

            // Close the migration indicator form.
            migrationForm?.Close();
        }
        private static void WaitForMigrationThenRestart()
        {
            string applicationPath = "JMMServer.exe";

            if (File.Exists(applicationPath))
            {
                MessageBox.Show("Application path = " + applicationPath);

                ProcessStartInfo Info = new ProcessStartInfo();
                Info.Arguments = "/C ping 127.0.0.1 -n 2 && \"" + applicationPath + "\"";
                Info.WindowStyle = ProcessWindowStyle.Hidden;
                Info.CreateNoWindow = true;
                Info.FileName = "cmd.exe";
                Process.Start(Info);
                Environment.Exit(1);
            }
        }

        private static bool disabledSave = false;
        public static void SaveSettings()
        {
            if (disabledSave)
                return;
            lock (appSettings)
            {
                if (appSettings.Count == 1)
                    return;//Somehow debugging may fuck up the settings so this shit will eject
                string path = Path.Combine(ApplicationPath, "settings.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(appSettings));
            }
        }
        public static string AnimeXmlDirectory
        {
            get
            {
                string dir = Get("AnimeXmlDirectory");
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(ApplicationPath, "Anime_HTTP");
                    Set("AnimeXmlDirectory", dir);
                }
                return dir;
            }
            set
            {
                Set("AnimeXmlDirectory", value);
            }
        }


        public static string MyListDirectory
        {
            get
            {
                string dir = Get("MyListDirectory");
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(ApplicationPath, "MyList");
                    Set("MyListDirectory", dir);
                }
                return dir;
            }
            set
            {
                Set("MyListDirectory", value);
            }
        }

        public static string MySqliteDirectory
        {
            get
            {
                string dir = Get("MySqliteDirectory");
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(ApplicationPath, "SQLite");
                    Set("MySqliteDirectory", dir);
                }
                return dir;
            }
            set
            {
                Set("MySqliteDirectory", value);
            }
        }
        public static string DatabaseBackupDirectory
        {
            get
            {
                string dir = Get("DatabaseBackupDirectory");
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(ApplicationPath, "DatabaseBackup");
                    Set("DatabaseBackupDirectory", dir);
                }
                return dir;
            }
            set
            {
                Set("DatabaseBackupDirectory", value);
            }
        }

        public static string JMMServerPort
        {
            get
            {

                string serverPort = Get("JMMServerPort");
                if (string.IsNullOrEmpty(serverPort))
                {
                    serverPort = "8111";
                    Set("JMMServerPort", serverPort);
                }
                return serverPort;
            }
            set { Set("JMMServerPort", value); }
        }

        public static string JMMServerFilePort
        {
            get
            {
                

                string serverPort = Get("JMMServerFilePort");
                if (string.IsNullOrEmpty(serverPort))
                {
                    serverPort = "8112";
                    Set("JMMServerFilePort", serverPort);
                }

                return serverPort;
            }
            set { Set("JMMServerFilePort", value); }
        }
        public static string PluginAutoWatchThreshold
        {
            get
            {
                

                string th = Get("PluginAutoWatchThreshold");
                if (string.IsNullOrEmpty(th))
                {
                    th = "0.89";
                    Set("PluginAutoWatchThreshold", th);
                }

                return th;
            }
            set { Set("PluginAutoWatchThreshold", value); }
        }
        public static string PlexThumbnailAspects
        {
            get
            {
                
                string thumbaspect = Get("PlexThumbnailAspects");
                if (string.IsNullOrEmpty(thumbaspect))
                {
                    thumbaspect = "Default, 0.6667, IOS, 1.0, Android, 1.3333";
                    Set("PlexThumbnailAspects", thumbaspect);
                }

                return thumbaspect;
            }
            set { Set("PlexThumbnailAspect", value); }
        }

        public static string Culture
        {
            get
            {
                

                string cult = Get("Culture");
                if (string.IsNullOrEmpty(cult))
                {
                    // default value
                    cult = "en";
                    Set("Culture", cult);
                }
                return cult;
            }
            set { Set("Culture", value); }
        }

        /// <summary>
        /// FirstRun idicates if DB was configured or not, as it needed as backend for user authentication
        /// </summary>
        public static bool FirstRun
        {
            get
            {
                
                bool val = true;
                if ( !string.IsNullOrEmpty(Get("FirstRun")))
                { bool.TryParse(Get("FirstRun"), out val); }
                else
                { FirstRun = val; }
                return val;
            }
            set { Set("FirstRun", value.ToString()); }
        }

        #region LogRotator

        public static bool RotateLogs
        {
            get
            {
                bool val = true;
                if (!string.IsNullOrEmpty(Get("RotateLogs")))
                { bool.TryParse(Get("RotateLogs"), out val); }
                else
                { RotateLogs = val; }
                return val;

            }
            set { Set("RotateLogs", value.ToString()); }
        }

        public static bool RotateLogs_Zip
        {
            get
            {
                bool val = true;
                if (!string.IsNullOrEmpty(Get("RotateLogs_Zip")))
                { bool.TryParse(Get("RotateLogs_Zip"), out val); }
                else
                { RotateLogs = val; }
                return val;
            }
            set { Set("RotateLogs_Zip", value.ToString()); }
        }

        public static bool RotateLogs_Delete
        {
            get
            {
                bool val = true;
                if (!string.IsNullOrEmpty(Get("RotateLogs_Delete")))
                { bool.TryParse(Get("RotateLogs_Delete"), out val); }
                else
                { RotateLogs = val; }
                return val;
            }
            set { Set("RotateLogs_Delete", value.ToString()); }
        }

        public static string RotateLogs_Delete_Days
        {
            get
            {
                string val = "90";
                if (string.IsNullOrEmpty(Get("RotateLogs_Delete_Days")))
                { RotateLogs_Delete_Days = val; }
                return Get("RotateLogs_Delete_Days");

            }
            set { Set("RotateLogs_Delete_Days", value); }
        }

        #endregion

        #region Database

        public static string DatabaseType
        {
            get
            {
                
                return Get("DatabaseType");
            }
            set { Set("DatabaseType", value); }
        }

        public static string DatabaseServer
        {
            get
            {
                
                return Get("SQLServer_DatabaseServer");
            }
            set { Set("SQLServer_DatabaseServer", value); }
        }

        public static string DatabaseName
        {
            get
            {
                
                return Get("SQLServer_DatabaseName");
            }
            set { Set("SQLServer_DatabaseName", value); }
        }

        public static string DatabaseUsername
        {
            get
            {
                
                return Get("SQLServer_Username");
            }
            set { Set("SQLServer_Username", value); }
        }

        public static string DatabasePassword
        {
            get
            {
                
                return Get("SQLServer_Password");
            }
            set { Set("SQLServer_Password", value); }
        }

        public static string DatabaseFile
        {
            get
            {
                
                return Get("SQLite_DatabaseFile");
            }
            set { Set("SQLite_DatabaseFile", value); }
        }

        public static string MySQL_Hostname
        {
            get
            {
                
                return Get("MySQL_Hostname");
            }
            set { Set("MySQL_Hostname", value); }
        }

        public static string MySQL_SchemaName
        {
            get
            {
                
                return Get("MySQL_SchemaName");
            }
            set { Set("MySQL_SchemaName", value); }
        }

        public static string MySQL_Username
        {
            get
            {
                
                return Get("MySQL_Username");
            }
            set { Set("MySQL_Username", value); }
        }

        public static string MySQL_Password
        {
            get
            {
                
                return Get("MySQL_Password");
            }
            set { Set("MySQL_Password", value); }
        }

        #endregion

        #region AniDB

        public static string AniDB_Username
        {
            get
            {
                
                return Get("AniDB_Username");
            }
            set { Set("AniDB_Username", value); }
        }

        public static string AniDB_Password
        {
            get
            {
                
                return Get("AniDB_Password");
            }
            set { Set("AniDB_Password", value); }
        }

        public static string AniDB_ServerAddress
        {
            get
            {
                
                return Get("AniDB_ServerAddress");
            }
            set { Set("AniDB_ServerAddress", value); }
        }

        public static string AniDB_ServerPort
        {
            get
            {
                
                return Get("AniDB_ServerPort");
            }
            set { Set("AniDB_ServerPort", value); }
        }

        public static string AniDB_ClientPort
        {
            get
            {
                
                return Get("AniDB_ClientPort");
            }
            set { Set("AniDB_ClientPort", value); }
        }

        public static string AniDB_AVDumpKey
        {
            get
            {
                
                return Get("AniDB_AVDumpKey");
            }
            set { Set("AniDB_AVDumpKey", value); }
        }

        public static string AniDB_AVDumpClientPort
        {
            get
            {
                
                return Get("AniDB_AVDumpClientPort");
            }
            set { Set("AniDB_AVDumpClientPort", value); }
        }

        public static bool AniDB_DownloadRelatedAnime
        {
            get
            {
                
                bool download = false;
                bool.TryParse(Get("AniDB_DownloadRelatedAnime"), out download);
                return download;
            }
            set { Set("AniDB_DownloadRelatedAnime", value.ToString()); }
        }

        public static bool AniDB_DownloadSimilarAnime
        {
            get
            {
                
                bool download = false;
                bool.TryParse(Get("AniDB_DownloadSimilarAnime"), out download);
                return download;
            }
            set { Set("AniDB_DownloadSimilarAnime", value.ToString()); }
        }

        public static bool AniDB_DownloadReviews
        {
            get
            {
                
                bool download = false;
                bool.TryParse(Get("AniDB_DownloadReviews"), out download);
                return download;
            }
            set { Set("AniDB_DownloadReviews", value.ToString()); }
        }

        public static bool AniDB_DownloadReleaseGroups
        {
            get
            {
                
                bool download = false;
                bool.TryParse(Get("AniDB_DownloadReleaseGroups"), out download);
                return download;
            }
            set { Set("AniDB_DownloadReleaseGroups", value.ToString()); }
        }

        public static bool AniDB_MyList_AddFiles
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("AniDB_MyList_AddFiles"), out val);
                return val;
            }
            set { Set("AniDB_MyList_AddFiles", value.ToString()); }
        }

        public static AniDBFileStatus AniDB_MyList_StorageState
        {
            get
            {
                
                int val = 1;
                int.TryParse(Get("AniDB_MyList_StorageState"), out val);

                return (AniDBFileStatus) val;
            }
            set { Set("AniDB_MyList_StorageState", ((int) value).ToString()); }
        }

        public static AniDBFileDeleteType AniDB_MyList_DeleteType
        {
            get
            {
                
                int val = 0;
                int.TryParse(Get("AniDB_MyList_DeleteType"), out val);

                return (AniDBFileDeleteType) val;
            }
            set { Set("AniDB_MyList_DeleteType", ((int) value).ToString()); }
        }

        public static bool AniDB_MyList_ReadUnwatched
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("AniDB_MyList_ReadUnwatched"), out val);
                return val;
            }
            set { Set("AniDB_MyList_ReadUnwatched", value.ToString()); }
        }

        public static bool AniDB_MyList_ReadWatched
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("AniDB_MyList_ReadWatched"), out val);
                return val;
            }
            set { Set("AniDB_MyList_ReadWatched", value.ToString()); }
        }

        public static bool AniDB_MyList_SetWatched
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("AniDB_MyList_SetWatched"), out val);
                return val;
            }
            set { Set("AniDB_MyList_SetWatched", value.ToString()); }
        }

        public static bool AniDB_MyList_SetUnwatched
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("AniDB_MyList_SetUnwatched"), out val);
                return val;
            }
            set { Set("AniDB_MyList_SetUnwatched", value.ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_MyList_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("AniDB_MyList_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.Never; // default value
            }
            set { Set("AniDB_MyList_UpdateFrequency", ((int) value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_Calendar_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("AniDB_Calendar_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set { Set("AniDB_Calendar_UpdateFrequency", ((int) value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_Anime_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("AniDB_Anime_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set { Set("AniDB_Anime_UpdateFrequency", ((int) value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_MyListStats_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("AniDB_MyListStats_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.Never; // default value
            }
            set { Set("AniDB_MyListStats_UpdateFrequency", ((int) value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_File_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("AniDB_File_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.Daily; // default value
            }
            set { Set("AniDB_File_UpdateFrequency", ((int) value).ToString()); }
        }

        public static bool AniDB_DownloadCharacters
        {
            get
            {
                
                bool val = true;
                if (!bool.TryParse(Get("AniDB_DownloadCharacters"), out val))
                    val = true; // default
                return val;
            }
            set { Set("AniDB_DownloadCharacters", value.ToString()); }
        }

        public static bool AniDB_DownloadCreators
        {
            get
            {
                
                bool val = true;
                if (!bool.TryParse(Get("AniDB_DownloadCreators"), out val))
                    val = true; // default
                return val;
            }
            set { Set("AniDB_DownloadCreators", value.ToString()); }
        }

        #endregion

        #region Web Cache

        public static string WebCache_Address
        {
            get
            {
                
                return Get("WebCache_Address");
            }
            set { Set("WebCache_Address", value); }
        }

        public static bool WebCache_Anonymous
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("WebCache_Anonymous"), out val);
                return val;
            }
            set { Set("WebCache_Anonymous", value.ToString()); }
        }

        public static bool WebCache_XRefFileEpisode_Get
        {
            get
            {
                
                bool usecache = false;
                bool.TryParse(Get("WebCache_XRefFileEpisode_Get"), out usecache);
                return usecache;
            }
            set { Set("WebCache_XRefFileEpisode_Get", value.ToString()); }
        }

        public static bool WebCache_XRefFileEpisode_Send
        {
            get
            {
                
                bool usecache = false;
                bool.TryParse(Get("WebCache_XRefFileEpisode_Send"), out usecache);
                return usecache;
            }
            set { Set("WebCache_XRefFileEpisode_Send", value.ToString()); }
        }

        public static bool WebCache_TvDB_Get
        {
            get
            {
                
                bool usecache = true;
                if (bool.TryParse(Get("WebCache_TvDB_Get"), out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set { Set("WebCache_TvDB_Get", value.ToString()); }
        }

        public static bool WebCache_TvDB_Send
        {
            get
            {
                
                bool usecache = true;
                if (bool.TryParse(Get("WebCache_TvDB_Send"), out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set { Set("WebCache_TvDB_Send", value.ToString()); }
        }

        public static bool WebCache_Trakt_Get
        {
            get
            {
                
                bool usecache = true;
                if (bool.TryParse(Get("WebCache_Trakt_Get"), out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set { Set("WebCache_Trakt_Get", value.ToString()); }
        }

        public static bool WebCache_Trakt_Send
        {
            get
            {
                
                bool usecache = true;
                if (bool.TryParse(Get("WebCache_Trakt_Send"), out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set { Set("WebCache_Trakt_Send", value.ToString()); }
        }

        public static bool WebCache_MAL_Get
        {
            get
            {
                
                bool usecache = true;
                if (bool.TryParse(Get("WebCache_MAL_Get"), out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set { Set("WebCache_MAL_Get", value.ToString()); }
        }

        public static bool WebCache_MAL_Send
        {
            get
            {
                
                bool usecache = true;
                if (bool.TryParse(Get("WebCache_MAL_Send"), out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set { Set("WebCache_MAL_Send", value.ToString()); }
        }

        public static bool WebCache_UserInfo
        {
            get
            {
                
                bool usecache = false;
                if (bool.TryParse(Get("WebCache_UserInfo"), out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set { Set("WebCache_UserInfo", value.ToString()); }
        }

        #endregion

        #region TvDB

        public static bool TvDB_AutoFanart
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("TvDB_AutoFanart"), out val);
                return val;
            }
            set { Set("TvDB_AutoFanart", value.ToString()); }
        }

        public static int TvDB_AutoFanartAmount
        {
            get
            {
                
                int val = 0;
                int.TryParse(Get("TvDB_AutoFanartAmount"), out val);
                return val;
            }
            set { Set("TvDB_AutoFanartAmount", value.ToString()); }
        }

        public static bool TvDB_AutoWideBanners
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("TvDB_AutoWideBanners"), out val);
                return val;
            }
            set { Set("TvDB_AutoWideBanners", value.ToString()); }
        }

        public static int TvDB_AutoWideBannersAmount
        {
            get
            {
                
                int val = 0;
                if (!int.TryParse(Get("TvDB_AutoWideBannersAmount"), out val))
                    val = 10; // default
                return val;
            }
            set { Set("TvDB_AutoWideBannersAmount", value.ToString()); }
        }

        public static bool TvDB_AutoPosters
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("TvDB_AutoPosters"), out val);
                return val;
            }
            set { Set("TvDB_AutoPosters", value.ToString()); }
        }

        public static int TvDB_AutoPostersAmount
        {
            get
            {
                
                int val = 0;
                if (!int.TryParse(Get("TvDB_AutoPostersAmount"), out val))
                    val = 10; // default
                return val;
            }
            set { Set("TvDB_AutoPostersAmount", value.ToString()); }
        }

        public static ScheduledUpdateFrequency TvDB_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("TvDB_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set { Set("TvDB_UpdateFrequency", ((int) value).ToString()); }
        }

        public static string TvDB_Language
        {
            get
            {
                
                string language = Get("TvDB_Language");
                if (string.IsNullOrEmpty(language))
                    return "en";
                else
                    return language;
            }
            set { Set("TvDB_Language", value); }
        }

        #endregion

        #region MovieDB

        public static bool MovieDB_AutoFanart
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("MovieDB_AutoFanart"), out val);
                return val;
            }
            set { Set("MovieDB_AutoFanart", value.ToString()); }
        }

        public static int MovieDB_AutoFanartAmount
        {
            get
            {
                
                int val = 0;
                int.TryParse(Get("MovieDB_AutoFanartAmount"), out val);
                return val;
            }
            set { Set("MovieDB_AutoFanartAmount", value.ToString()); }
        }

        public static bool MovieDB_AutoPosters
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("MovieDB_AutoPosters"), out val);
                return val;
            }
            set { Set("MovieDB_AutoPosters", value.ToString()); }
        }

        public static int MovieDB_AutoPostersAmount
        {
            get
            {
                
                int val = 0;
                if (!int.TryParse(Get("MovieDB_AutoPostersAmount"), out val))
                    val = 10; // default
                return val;
            }
            set { Set("MovieDB_AutoPostersAmount", value.ToString()); }
        }

        #endregion

        #region Import Settings

        public static string VideoExtensions
        {
            get
            {
                
                return Get("VideoExtensions");
            }
            set { Set("VideoExtensions", value); }
        }

        public static RenamingLanguage DefaultSeriesLanguage
        {
            get
            {
                RenamingLanguage rl = RenamingLanguage.Romaji;
                

                string rls = Get("DefaultSeriesLanguage");
                if (string.IsNullOrEmpty(rls)) return rl;

                rl = (RenamingLanguage) int.Parse(rls);

                return rl;
            }
            set { Set("DefaultSeriesLanguage", ((int) value).ToString()); }
        }

        public static RenamingLanguage DefaultEpisodeLanguage
        {
            get
            {
                RenamingLanguage rl = RenamingLanguage.Romaji;
                

                string rls = Get("DefaultEpisodeLanguage");
                if (string.IsNullOrEmpty(rls)) return rl;

                rl = (RenamingLanguage) int.Parse(rls);

                return rl;
            }
            set { Set("DefaultEpisodeLanguage", ((int) value).ToString()); }
        }

        public static bool RunImportOnStart
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("RunImportOnStart"), out val);
                return val;
            }
            set { Set("RunImportOnStart", value.ToString()); }
        }

        public static bool ScanDropFoldersOnStart
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("ScanDropFoldersOnStart"), out val);
                return val;
            }
            set { Set("ScanDropFoldersOnStart", value.ToString()); }
        }

        public static bool Hash_CRC32
        {
            get
            {
                
                bool bval = false;
                bool.TryParse(Get("Hash_CRC32"), out bval);
                return bval;
            }
            set { Set("Hash_CRC32", value.ToString()); }
        }

        public static bool Hash_MD5
        {
            get
            {
                
                bool bval = false;
                bool.TryParse(Get("Hash_MD5"), out bval);
                return bval;
            }
            set { Set("Hash_MD5", value.ToString()); }
        }

        public static bool ExperimentalUPnP
        {
            get
            {
                
                bool bval = false;
                bool.TryParse(Get("ExperimentalUPnP"), out bval);
                return bval;
            }
            set { Set("ExperimentalUPnP", value.ToString()); }
        }

        public static bool Hash_SHA1
        {
            get
            {
                
                bool bval = false;
                bool.TryParse(Get("Hash_SHA1"), out bval);
                return bval;
            }
            set { Set("Hash_SHA1", value.ToString()); }
        }

        public static bool Import_UseExistingFileWatchedStatus
        {
            get
            {
                
                bool bval = false;
                bool.TryParse(Get("Import_UseExistingFileWatchedStatus"), out bval);
                return bval;
            }
            set { Set("Import_UseExistingFileWatchedStatus", value.ToString()); }
        }

        #endregion

        public static bool AutoGroupSeries
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("AutoGroupSeries"), out val);
                return val;
            }
            set { Set("AutoGroupSeries", value.ToString()); }
        }

        public static string AutoGroupSeriesRelationExclusions
        {
            get
            {
                
                string val = "same setting|alternative setting|character|other";
                try
                {
                    val = Get("AutoGroupSeriesRelationExclusions");
                }
                catch (Exception e)
                {
                }
                return val;
            }
            set { Set("AutoGroupSeriesRelationExclusions", value); }
        }

        public static string LanguagePreference
        {
            get
            {
                
                return Get("LanguagePreference");
            }
            set { Set("LanguagePreference", value); }
        }

        public static bool LanguageUseSynonyms
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("LanguageUseSynonyms"), out val);
                return val;
            }
            set { Set("LanguageUseSynonyms", value.ToString()); }
        }
        public static int CloudWatcherTime
        {
            get
            {
                
                int val;
                int.TryParse(Get("CloudWatcherTime"), out val);
                if (val == 0)
                    val = 3;
                return val;
            }
            set { Set("CloudWatcherTime", ((int)value).ToString()); }
        }
        public static DataSourceType EpisodeTitleSource
        {
            get
            {
                
                int val = 0;
                int.TryParse(Get("EpisodeTitleSource"), out val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                else
                    return (DataSourceType) val;
            }
            set { Set("EpisodeTitleSource", ((int) value).ToString()); }
        }

        public static DataSourceType SeriesDescriptionSource
        {
            get
            {
                
                int val = 0;
                int.TryParse(Get("SeriesDescriptionSource"), out val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                else
                    return (DataSourceType) val;
            }
            set { Set("SeriesDescriptionSource", ((int) value).ToString()); }
        }

        public static DataSourceType SeriesNameSource
        {
            get
            {
                
                int val = 0;
                int.TryParse(Get("SeriesNameSource"), out val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                else
                    return (DataSourceType) val;
            }
            set { Set("SeriesNameSource", ((int) value).ToString()); }
        }

        public static string ImagesPath
        {
            get
            {

                return Get("ImagesPath");
            }
            set
            {
                Set("ImagesPath", value);
                ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();
            }
        }


        private static string BaseImagesPath => Get("BaseImagesPath");


        private static bool BaseImagesPathIsDefault
        {
            get
            {
                
                string basePath = Get("BaseImagesPathIsDefault");
                if (!string.IsNullOrEmpty(basePath))
                {
                    bool val = true;
                    bool.TryParse(basePath, out val);
                    return val;
                }
                else return true;
            }

        }

        public static string VLCLocation
        {
            get
            {
                
                return Get("VLCLocation");
            }
            set
            {
                Set("VLCLocation", value);
                ServerState.Instance.VLCLocation = value;
            }
        }

        public static bool MinimizeOnStartup
        {
            get
            {
                
                bool val = false;
                bool.TryParse(Get("MinimizeOnStartup"), out val);
                return val;
            }
            set { Set("MinimizeOnStartup", value.ToString()); }
        }

        #region Trakt

        public static bool Trakt_IsEnabled
        {
            get
            {
                
                bool val = true;
                if (!bool.TryParse(Get("Trakt_IsEnabled"), out val))
                    val = true;
                return val;
            }
            set { Set("Trakt_IsEnabled", value.ToString()); }
        }

        public static string Trakt_PIN { get; set; }

        public static string Trakt_AuthToken
        {
            get
            {
                
                return Get("Trakt_AuthToken");
            }
            set { Set("Trakt_AuthToken", value); }
        }

        public static string Trakt_RefreshToken
        {
            get
            {
                
                return Get("Trakt_RefreshToken");
            }
            set { Set("Trakt_RefreshToken", value); }
        }

        public static string Trakt_TokenExpirationDate
        {
            get
            {
                
                return Get("Trakt_TokenExpirationDate");
            }
            set { Set("Trakt_TokenExpirationDate", value); }
        }

        public static ScheduledUpdateFrequency Trakt_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("Trakt_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.Daily; // default value
            }
            set { Set("Trakt_UpdateFrequency", ((int) value).ToString()); }
        }

        public static ScheduledUpdateFrequency Trakt_SyncFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("Trakt_SyncFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.Never; // default value
            }
            set { Set("Trakt_SyncFrequency", ((int) value).ToString()); }
        }

        public static bool Trakt_DownloadFanart
        {
            get
            {
                
                bool val = true;
                if (!bool.TryParse(Get("Trakt_DownloadFanart"), out val))
                    val = true; // default
                return val;
            }
            set { Set("Trakt_DownloadFanart", value.ToString()); }
        }

        public static bool Trakt_DownloadPosters
        {
            get
            {
                
                bool val = true;
                if (!bool.TryParse(Get("Trakt_DownloadPosters"), out val))
                    val = true; // default
                return val;
            }
            set { Set("Trakt_DownloadPosters", value.ToString()); }
        }

        public static bool Trakt_DownloadEpisodes
        {
            get
            {
                
                bool val = true;
                if (!bool.TryParse(Get("Trakt_DownloadEpisodes"), out val))
                    val = true; // default
                return val;
            }
            set { Set("Trakt_DownloadEpisodes", value.ToString()); }
        }

        #endregion

        #region MAL

        public static string MAL_Username
        {
            get
            {
                
                return Get("MAL_Username");
            }
            set { Set("MAL_Username", value); }
        }

        public static string MAL_Password
        {
            get
            {
                
                return Get("MAL_Password");
            }
            set { Set("MAL_Password", value); }
        }

        public static ScheduledUpdateFrequency MAL_UpdateFrequency
        {
            get
            {
                
                int val = 1;
                if (int.TryParse(Get("MAL_UpdateFrequency"), out val))
                    return (ScheduledUpdateFrequency) val;
                else
                    return ScheduledUpdateFrequency.Daily; // default value
            }
            set { Set("MAL_UpdateFrequency", ((int) value).ToString()); }
        }

        public static bool MAL_NeverDecreaseWatchedNums
        {
            get
            {
                
                string wtchNum = Get("MAL_NeverDecreaseWatchedNums");
                if (!string.IsNullOrEmpty(wtchNum))
                {
                    bool val = true;
                    bool.TryParse(wtchNum, out val);
                    return val;
                }
                else return true;
            }
            set { Set("MAL_NeverDecreaseWatchedNums", value.ToString()); }
        }

        #endregion

        public static string WebCacheAuthKey
        {
            get
            {
                
                return Get("WebCacheAuthKey");
            }
            set { Set("WebCacheAuthKey", value); }
        }

        public static Contract_ServerSettings ToContract()
        {
            Contract_ServerSettings contract = new Contract_ServerSettings();

            contract.AniDB_Username = ServerSettings.AniDB_Username;
            contract.AniDB_Password = ServerSettings.AniDB_Password;
            contract.AniDB_ServerAddress = ServerSettings.AniDB_ServerAddress;
            contract.AniDB_ServerPort = ServerSettings.AniDB_ServerPort;
            contract.AniDB_ClientPort = ServerSettings.AniDB_ClientPort;
            contract.AniDB_AVDumpClientPort = ServerSettings.AniDB_AVDumpClientPort;
            contract.AniDB_AVDumpKey = ServerSettings.AniDB_AVDumpKey;

            contract.AniDB_DownloadRelatedAnime = ServerSettings.AniDB_DownloadRelatedAnime;
            contract.AniDB_DownloadSimilarAnime = ServerSettings.AniDB_DownloadSimilarAnime;
            contract.AniDB_DownloadReviews = ServerSettings.AniDB_DownloadReviews;
            contract.AniDB_DownloadReleaseGroups = ServerSettings.AniDB_DownloadReleaseGroups;

            contract.AniDB_MyList_AddFiles = ServerSettings.AniDB_MyList_AddFiles;
            contract.AniDB_MyList_StorageState = (int) ServerSettings.AniDB_MyList_StorageState;
            contract.AniDB_MyList_DeleteType = (int) ServerSettings.AniDB_MyList_DeleteType;
            contract.AniDB_MyList_ReadWatched = ServerSettings.AniDB_MyList_ReadWatched;
            contract.AniDB_MyList_ReadUnwatched = ServerSettings.AniDB_MyList_ReadUnwatched;
            contract.AniDB_MyList_SetWatched = ServerSettings.AniDB_MyList_SetWatched;
            contract.AniDB_MyList_SetUnwatched = ServerSettings.AniDB_MyList_SetUnwatched;

            contract.AniDB_MyList_UpdateFrequency = (int) ServerSettings.AniDB_MyList_UpdateFrequency;
            contract.AniDB_Calendar_UpdateFrequency = (int) ServerSettings.AniDB_Calendar_UpdateFrequency;
            contract.AniDB_Anime_UpdateFrequency = (int) ServerSettings.AniDB_Anime_UpdateFrequency;
            contract.AniDB_MyListStats_UpdateFrequency = (int) ServerSettings.AniDB_MyListStats_UpdateFrequency;
            contract.AniDB_File_UpdateFrequency = (int) ServerSettings.AniDB_File_UpdateFrequency;

            contract.AniDB_DownloadCharacters = ServerSettings.AniDB_DownloadCharacters;
            contract.AniDB_DownloadCreators = ServerSettings.AniDB_DownloadCreators;

            // Web Cache
            contract.WebCache_Address = ServerSettings.WebCache_Address;
            contract.WebCache_Anonymous = ServerSettings.WebCache_Anonymous;
            contract.WebCache_XRefFileEpisode_Get = ServerSettings.WebCache_XRefFileEpisode_Get;
            contract.WebCache_XRefFileEpisode_Send = ServerSettings.WebCache_XRefFileEpisode_Send;
            contract.WebCache_TvDB_Get = ServerSettings.WebCache_TvDB_Get;
            contract.WebCache_TvDB_Send = ServerSettings.WebCache_TvDB_Send;
            contract.WebCache_Trakt_Get = ServerSettings.WebCache_Trakt_Get;
            contract.WebCache_Trakt_Send = ServerSettings.WebCache_Trakt_Send;
            contract.WebCache_MAL_Get = ServerSettings.WebCache_MAL_Get;
            contract.WebCache_MAL_Send = ServerSettings.WebCache_MAL_Send;
            contract.WebCache_UserInfo = ServerSettings.WebCache_UserInfo;

            // TvDB
            contract.TvDB_AutoFanart = ServerSettings.TvDB_AutoFanart;
            contract.TvDB_AutoFanartAmount = ServerSettings.TvDB_AutoFanartAmount;
            contract.TvDB_AutoPosters = ServerSettings.TvDB_AutoPosters;
            contract.TvDB_AutoPostersAmount = ServerSettings.TvDB_AutoPostersAmount;
            contract.TvDB_AutoWideBanners = ServerSettings.TvDB_AutoWideBanners;
            contract.TvDB_AutoWideBannersAmount = ServerSettings.TvDB_AutoWideBannersAmount;
            contract.TvDB_UpdateFrequency = (int) ServerSettings.TvDB_UpdateFrequency;
            contract.TvDB_Language = ServerSettings.TvDB_Language;

            // MovieDB
            contract.MovieDB_AutoFanart = ServerSettings.MovieDB_AutoFanart;
            contract.MovieDB_AutoFanartAmount = ServerSettings.MovieDB_AutoFanartAmount;
            contract.MovieDB_AutoPosters = ServerSettings.MovieDB_AutoPosters;
            contract.MovieDB_AutoPostersAmount = ServerSettings.MovieDB_AutoPostersAmount;

            // Import settings
            contract.VideoExtensions = ServerSettings.VideoExtensions;
            contract.AutoGroupSeries = ServerSettings.AutoGroupSeries;
            contract.AutoGroupSeriesRelationExclusions = ServerSettings.AutoGroupSeriesRelationExclusions;
            contract.Import_UseExistingFileWatchedStatus = ServerSettings.Import_UseExistingFileWatchedStatus;
            contract.RunImportOnStart = ServerSettings.RunImportOnStart;
            contract.ScanDropFoldersOnStart = ServerSettings.ScanDropFoldersOnStart;
            contract.Hash_CRC32 = ServerSettings.Hash_CRC32;
            contract.Hash_MD5 = ServerSettings.Hash_MD5;
            contract.Hash_SHA1 = ServerSettings.Hash_SHA1;

            // Language
            contract.LanguagePreference = ServerSettings.LanguagePreference;
            contract.LanguageUseSynonyms = ServerSettings.LanguageUseSynonyms;
            contract.EpisodeTitleSource = (int) ServerSettings.EpisodeTitleSource;
            contract.SeriesDescriptionSource = (int) ServerSettings.SeriesDescriptionSource;
            contract.SeriesNameSource = (int) ServerSettings.SeriesNameSource;

            // trakt
            contract.Trakt_IsEnabled = ServerSettings.Trakt_IsEnabled;
            contract.Trakt_AuthToken = ServerSettings.Trakt_AuthToken;
            contract.Trakt_RefreshToken = ServerSettings.Trakt_RefreshToken;
            contract.Trakt_TokenExpirationDate = ServerSettings.Trakt_TokenExpirationDate;
            contract.Trakt_UpdateFrequency = (int) ServerSettings.Trakt_UpdateFrequency;
            contract.Trakt_SyncFrequency = (int) ServerSettings.Trakt_SyncFrequency;
            contract.Trakt_DownloadEpisodes = ServerSettings.Trakt_DownloadEpisodes;
            contract.Trakt_DownloadFanart = ServerSettings.Trakt_DownloadFanart;
            contract.Trakt_DownloadPosters = ServerSettings.Trakt_DownloadPosters;

            // MAL
            contract.MAL_Username = ServerSettings.MAL_Username;
            contract.MAL_Password = ServerSettings.MAL_Password;
            contract.MAL_UpdateFrequency = (int) ServerSettings.MAL_UpdateFrequency;
            contract.MAL_NeverDecreaseWatchedNums = ServerSettings.MAL_NeverDecreaseWatchedNums;


            return contract;
        }

        public static void DebugSettingsToLog()
        {
            #region System Info

            logger.Info("-------------------- SYSTEM INFO -----------------------");

            System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
            try
            {
                if (a != null)
                {
                    logger.Info(string.Format("JMM Server Version: v{0}", Utils.GetApplicationVersion(a)));
                }
            }
            catch (Exception ex)
            {
                // oopps, can't create file
                logger.Warn("Error in log: {0}", ex.ToString());
            }

            try
            {
                logger.Info(string.Format("Database Version: {0}", DatabaseExtensions.Instance.GetDatabaseVersion()));
            }
            catch (Exception ex)
            {
                // oopps, can't create file
                logger.Warn("Error in log: {0}", ex.Message);
            }

            logger.Info(string.Format("Operating System: {0}", Utils.GetOSInfo()));

            string screenSize = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width.ToString() + "x" +
                                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height.ToString();
            logger.Info(string.Format("Screen Size: {0}", screenSize));


            try
            {
                string mediaInfoVersion = "**** MediaInfo - DLL Not found *****";

                string mediaInfoPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                FileInfo fi = new FileInfo(mediaInfoPath);
                mediaInfoPath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "MediaInfo.dll");

                if (File.Exists(mediaInfoPath))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(mediaInfoPath);
                    mediaInfoVersion = string.Format("MediaInfo DLL {0}.{1}.{2}.{3} ({4})", fvi.FileMajorPart,
                        fvi.FileMinorPart,
                        fvi.FileBuildPart, fvi.FilePrivatePart, mediaInfoPath);
                }
                logger.Info(mediaInfoVersion);

                string hasherInfoVersion = "**** Hasher - DLL NOT found *****";

                string fullHasherexepath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                fi = new FileInfo(fullHasherexepath);
                fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "hasher.dll");

                if (File.Exists(fullHasherexepath))
                    hasherInfoVersion = string.Format("Hasher DLL found at {0}", fullHasherexepath);
                logger.Info(hasherInfoVersion);
            }
            catch
            {
            }

            logger.Info("-------------------------------------------------------");

            #endregion

            logger.Info("----------------- SERVER SETTINGS ----------------------");

            logger.Info("DatabaseType: {0}", DatabaseType);
            logger.Info("MSSQL DatabaseServer: {0}", DatabaseServer);
            logger.Info("MSSQL DatabaseName: {0}", DatabaseName);
            logger.Info("MSSQL DatabaseUsername: {0}",
                string.IsNullOrEmpty(DatabaseUsername) ? "NOT SET" : "***HIDDEN***");
            logger.Info("MSSQL DatabasePassword: {0}",
                string.IsNullOrEmpty(DatabasePassword) ? "NOT SET" : "***HIDDEN***");

            logger.Info("SQLITE DatabaseFile: {0}", DatabaseFile);

            logger.Info("MySQL_Hostname: {0}", MySQL_Hostname);
            logger.Info("MySQL_SchemaName: {0}", MySQL_SchemaName);
            logger.Info("MySQL_Username: {0}", string.IsNullOrEmpty(MySQL_Username) ? "NOT SET" : "***HIDDEN***");
            logger.Info("MySQL_Password: {0}", string.IsNullOrEmpty(MySQL_Password) ? "NOT SET" : "***HIDDEN***");

            logger.Info("AniDB_Username: {0}", string.IsNullOrEmpty(AniDB_Username) ? "NOT SET" : "***HIDDEN***");
            logger.Info("AniDB_Password: {0}", string.IsNullOrEmpty(AniDB_Password) ? "NOT SET" : "***HIDDEN***");
            logger.Info("AniDB_ServerAddress: {0}", AniDB_ServerAddress);
            logger.Info("AniDB_ServerPort: {0}", AniDB_ServerPort);
            logger.Info("AniDB_ClientPort: {0}", AniDB_ClientPort);
            logger.Info("AniDB_AVDumpKey: {0}", string.IsNullOrEmpty(AniDB_AVDumpKey) ? "NOT SET" : "***HIDDEN***");
            logger.Info("AniDB_AVDumpClientPort: {0}", AniDB_AVDumpClientPort);
            logger.Info("AniDB_DownloadRelatedAnime: {0}", AniDB_DownloadRelatedAnime);
            logger.Info("AniDB_DownloadSimilarAnime: {0}", AniDB_DownloadSimilarAnime);
            logger.Info("AniDB_DownloadReviews: {0}", AniDB_DownloadReviews);
            logger.Info("AniDB_DownloadReleaseGroups: {0}", AniDB_DownloadReleaseGroups);
            logger.Info("AniDB_MyList_AddFiles: {0}", AniDB_MyList_AddFiles);
            logger.Info("AniDB_MyList_StorageState: {0}", AniDB_MyList_StorageState);
            logger.Info("AniDB_MyList_ReadUnwatched: {0}", AniDB_MyList_ReadUnwatched);
            logger.Info("AniDB_MyList_ReadWatched: {0}", AniDB_MyList_ReadWatched);
            logger.Info("AniDB_MyList_SetWatched: {0}", AniDB_MyList_SetWatched);
            logger.Info("AniDB_MyList_SetUnwatched: {0}", AniDB_MyList_SetUnwatched);
            logger.Info("AniDB_MyList_UpdateFrequency: {0}", AniDB_MyList_UpdateFrequency);
            logger.Info("AniDB_Calendar_UpdateFrequency: {0}", AniDB_Calendar_UpdateFrequency);
            logger.Info("AniDB_Anime_UpdateFrequency: {0}", AniDB_Anime_UpdateFrequency);


            logger.Info("WebCache_Address: {0}", WebCache_Address);
            logger.Info("WebCache_Anonymous: {0}", WebCache_Anonymous);
            logger.Info("WebCache_XRefFileEpisode_Get: {0}", WebCache_XRefFileEpisode_Get);
            logger.Info("WebCache_XRefFileEpisode_Send: {0}", WebCache_XRefFileEpisode_Send);
            logger.Info("WebCache_TvDB_Get: {0}", WebCache_TvDB_Get);
            logger.Info("WebCache_TvDB_Send: {0}", WebCache_TvDB_Send);
            logger.Info("WebCache_MAL_Get: {0}", WebCache_MAL_Get);
            logger.Info("WebCache_MAL_Send: {0}", WebCache_MAL_Send);

            logger.Info("TvDB_AutoFanart: {0}", TvDB_AutoFanart);
            logger.Info("TvDB_AutoFanartAmount: {0}", TvDB_AutoFanartAmount);
            logger.Info("TvDB_AutoWideBanners: {0}", TvDB_AutoWideBanners);
            logger.Info("TvDB_AutoPosters: {0}", TvDB_AutoPosters);
            logger.Info("TvDB_UpdateFrequency: {0}", TvDB_UpdateFrequency);
            logger.Info("TvDB_Language: {0}", TvDB_Language);

            logger.Info("MovieDB_AutoFanart: {0}", MovieDB_AutoFanart);
            logger.Info("MovieDB_AutoFanartAmount: {0}", MovieDB_AutoFanartAmount);
            logger.Info("MovieDB_AutoPosters: {0}", MovieDB_AutoPosters);

            logger.Info("VideoExtensions: {0}", VideoExtensions);
            logger.Info("DefaultSeriesLanguage: {0}", DefaultSeriesLanguage);
            logger.Info("DefaultEpisodeLanguage: {0}", DefaultEpisodeLanguage);
            logger.Info("RunImportOnStart: {0}", RunImportOnStart);
            logger.Info("Hash_CRC32: {0}", Hash_CRC32);
            logger.Info("Hash_MD5: {0}", Hash_MD5);
            logger.Info("Hash_SHA1: {0}", Hash_SHA1);
            logger.Info("Import_UseExistingFileWatchedStatus: {0}", Import_UseExistingFileWatchedStatus);

            logger.Info("Trakt_IsEnabled: {0}", Trakt_IsEnabled);
            logger.Info("Trakt_AuthToken: {0}", string.IsNullOrEmpty(Trakt_AuthToken) ? "NOT SET" : "***HIDDEN***");
            logger.Info("Trakt_RefreshToken: {0}", string.IsNullOrEmpty(Trakt_RefreshToken) ? "NOT SET" : "***HIDDEN***");
            logger.Info("Trakt_UpdateFrequency: {0}", Trakt_UpdateFrequency);
            logger.Info("Trakt_SyncFrequency: {0}", Trakt_SyncFrequency);

            logger.Info("AutoGroupSeries: {0}", AutoGroupSeries);
            logger.Info("AutoGroupSeriesRelationExclusions: {0}", AutoGroupSeriesRelationExclusions);
            logger.Info("LanguagePreference: {0}", LanguagePreference);
            logger.Info("LanguageUseSynonyms: {0}", LanguageUseSynonyms);
            logger.Info("EpisodeTitleSource: {0}", EpisodeTitleSource);
            logger.Info("SeriesDescriptionSource: {0}", SeriesDescriptionSource);
            logger.Info("SeriesNameSource: {0}", SeriesNameSource);
            logger.Info("BaseImagesPath: {0}", BaseImagesPath);
            logger.Info("BaseImagesPathIsDefault: {0}", BaseImagesPathIsDefault);


            logger.Info("-------------------------------------------------------");
        }
    }
}