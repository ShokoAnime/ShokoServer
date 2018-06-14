using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using AniDBAPI;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using NLog.Targets;
using Shoko.Commons.Properties;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.ImageDownload;
using Formatting = Newtonsoft.Json.Formatting;

namespace Shoko.Server
{
    public static class ServerSettings
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        internal static Dictionary<string, string> appSettings = new Dictionary<string, string>();
        private static bool migrationError;
        private static bool migrationActive;

        public static string Get(string key) => appSettings.ContainsKey(key) ? appSettings[key] : null;

        public static bool Set(string key, string value)
        {
            string orig = Get(key);
            if (null != value && value.Equals(orig))
                return false; // have new value and no change
            if (null != orig && orig.Equals(value))
                return false; // have old value and no change
            if (null == value && null == orig)
                return false; // no change

            appSettings[key] = value;
            SaveSettings();
            return true;
        }


        //in this way, we could host two ShokoServers int the same machine
        public static string DefaultInstance { get; set; } =
            Assembly.GetEntryAssembly().GetName().Name;

        public static string ApplicationPath
        {
            get
            {
                if (Utils.IsRunningOnMono())
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".shoko",
                        DefaultInstance);

                return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DefaultInstance);
            }
        }

        public static string DefaultImagePath => Path.Combine(ApplicationPath, "images");

        public class ReasonedEventArgs : EventArgs
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public string Reason { get; set; }
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public Exception Exception { get; set; }
        }

        public static event EventHandler<ReasonedEventArgs> ServerShutdown;
        //public static event EventHandler<ReasonedEventArgs> ServerError;
        public static void DoServerShutdown(ReasonedEventArgs args)
        {
            ServerShutdown?.Invoke(null, args);
        }
        /// <summary>
        /// Load setting from custom file - ex. read setting from backup
        /// </summary>
        /// <param name="tmp_setting_file">path to json setting file</param>
        /// <param name="delete_tmp_file">do you want delete file after being successful readed</param>
        public static void LoadSettingsFromFile(string tmp_setting_file, bool delete_tmp_file = false)
        {
            try
            {
                try
                {
                    //Reconfigure log file to applicationpath
                    var target = (FileTarget) LogManager.Configuration.FindTargetByName("file");
                    target.FileName = ApplicationPath + "/logs/${shortdate}.txt";
                    LogManager.ReconfigExistingLoggers();


                    disabledSave = true;
                    bool startedWithFreshConfig = false;

                    string programlocation =
                        Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    List<MigrationDirectory> migrationdirs = new List<MigrationDirectory>();

                    if (!string.IsNullOrEmpty(programlocation) && !string.IsNullOrEmpty(ApplicationPath))
                    {
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(programlocation, "SQLite"),
                            To = MySqliteDirectory
                        });
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(programlocation, "DatabaseBackup"),
                            To = DatabaseBackupDirectory
                        });
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(programlocation, "MyList"),
                            To = MyListDirectory
                        });
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(programlocation, "Anime_HTTP"),
                            To = AnimeXmlDirectory
                        });
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(programlocation, "logs"),
                            To = Path.Combine(ApplicationPath, "logs")
                        });
                    }

                    if (!string.IsNullOrEmpty(ApplicationPath))
                    {
                        //TODO add MessageBox If Failed
                        if (!Utils.GrantAccess(ApplicationPath))
                        {
                            logger.Error("Unable to gain permissions for program data. Try running as admin.");
                            return;
                        }
                    }
                    // Check and see if we have old JMMServer installation and add to migration if needed
                    string jmmServerInstallLocation = null;
                    if (!Utils.IsRunningOnMono())
                    {
                        jmmServerInstallLocation = (string)
                        Registry.GetValue(
                            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{898530ED-CFC7-4744-B2B8-A8D98A2FA06C}_is1",
                            "InstallLocation", null);
                    }


                    if (!string.IsNullOrEmpty(jmmServerInstallLocation))
                    {
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(jmmServerInstallLocation, "SQLite"),
                            To = MySqliteDirectory
                        });
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(jmmServerInstallLocation, "DatabaseBackup"),
                            To = DatabaseBackupDirectory
                        });
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(jmmServerInstallLocation, "MyList"),
                            To = MyListDirectory
                        });
                        migrationdirs.Add(new MigrationDirectory
                        {
                            From = Path.Combine(jmmServerInstallLocation, "Anime_HTTP"),
                            To = AnimeXmlDirectory
                        });

                        if (!string.IsNullOrEmpty(ApplicationPath))
                        {
                            migrationdirs.Add(new MigrationDirectory
                            {
                                From = Path.Combine(jmmServerInstallLocation, "logs"),
                                To = Path.Combine(ApplicationPath, "logs")
                            });
                        }
                    }
                    string path = string.Empty;

                    if (!string.IsNullOrEmpty(ApplicationPath))
                    {
                        path = Path.Combine(ApplicationPath, "settings.json");
                        if (tmp_setting_file != string.Empty)
                        {
                            path = tmp_setting_file;
                        }
                    }

                    bool settingsValid = false;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        Dictionary<string, string> previousSettings =
                            JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
                        if (delete_tmp_file)
                        {
                            File.Delete(tmp_setting_file);
                        }
                        if (HasAllNecessaryFields(previousSettings))
                        {
                            appSettings = previousSettings;
                            if (appSettings.ContainsKey("FileQualityFilterPreferences"))
                            {
                                try
                                {
                                    FileQualityPreferences prefs = JsonConvert.DeserializeObject<FileQualityPreferences>(
                                        appSettings["FileQualityFilterPreferences"], new StringEnumConverter());
                                    FileQualityFilter.Settings = prefs;
                                }
                                catch
                                {
                                    appSettings["FileQualityFilterPreferences"] = JsonConvert.SerializeObject(FileQualityFilter.Settings, Formatting.None, new StringEnumConverter());
                                }
                            }
                            else
                            {
                                appSettings["FileQualityFilterPreferences"] = JsonConvert.SerializeObject(FileQualityFilter.Settings, Formatting.None, new StringEnumConverter());
                            }
                            settingsValid = true;
                        }
                    }
                    if (!settingsValid)
                    {
                        startedWithFreshConfig = true;
                        LoadLegacySettingsFromFile(true);
                        if (appSettings.ContainsKey("FileQualityFilterPreferences"))
                        {
                            try
                            {
                                FileQualityPreferences prefs = JsonConvert.DeserializeObject<FileQualityPreferences>(
                                    appSettings["FileQualityFilterPreferences"], new StringEnumConverter());
                                FileQualityFilter.Settings = prefs;
                            }
                            catch
                            {
                                appSettings["FileQualityFilterPreferences"] = JsonConvert.SerializeObject(FileQualityFilter.Settings, Formatting.None, new StringEnumConverter());
                            }
                        }
                        else
                        {
                            appSettings["FileQualityFilterPreferences"] = JsonConvert.SerializeObject(FileQualityFilter.Settings, Formatting.None, new StringEnumConverter());
                        }
                    }


                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(Culture);
                    if (BaseImagesPathIsDefault || !Directory.Exists(BaseImagesPath))
                    {
                        if (!string.IsNullOrEmpty(programlocation))
                        {
                            migrationdirs.Add(new MigrationDirectory
                            {
                                From = Path.Combine(programlocation, "images"),
                                To = DefaultImagePath
                            });
                        }

                        if (!string.IsNullOrEmpty(jmmServerInstallLocation))
                        {
                            migrationdirs.Add(new MigrationDirectory
                            {
                                From = Path.Combine(jmmServerInstallLocation, "images"),
                                To = DefaultImagePath
                            });
                        }
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
                            logger.Info($"Will be migrating from: {m.From} to: {m.To}");
                            migrate = true;
                        }
                    }
                    if (migrate)
                    {
                        migrationActive = true;
                        if (!Utils.IsAdministrator())
                        {
                            logger.Info("Needed to migrate but user wasn't admin, restarting as admin.");
                            Utils.RestartAsAdmin();
                        }

                        logger.Info("User is admin so starting migration.");

                        try
                        {
                            // Show migration indicator
                            logger.Info("Migration showing indicator form..");

                            MigrationIndicatorForm();

                            logger.Info("Migration showed indicator form..");

                            if (!Directory.Exists(ApplicationPath))
                            {
                                logger.Info("Migration creating application path: " + ApplicationPath);
                                Directory.CreateDirectory(ApplicationPath);
                                logger.Info("Migration created application path: " + ApplicationPath);
                            }
                            //TODO add MessageBox If Failed
                            if (!Utils.GrantAccess(ApplicationPath))
                            {
                                logger.Error("Unable to gain permissions for program data. Try running as admin.");
                                return;
                            }
                            disabledSave = false;

                            if (!string.IsNullOrEmpty(DatabaseFile))
                            {
                                // Migrate sqlite db file if necessary
                                if (DatabaseFile.Contains(programlocation) && !string.IsNullOrEmpty(ApplicationPath))
                                {
                                    string dbname = Path.GetFileName(DatabaseFile);
                                    DatabaseFile = Path.Combine(ApplicationPath, dbname);
                                }
                            }
                            else
                            {
                                logger.Error(
                                    "Error occured during LoadSettingsFromFile() , DatabaseFile is null or empty");
                            }

                            logger.Info("Migration saving settings..");
                            SaveSettings();
                            logger.Info("Migration saved settings");

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
                            Utils.ShowErrorMessage(Resources.Migration_SettingsError + " ",
                                e.ToString());
                            logger.Error(e);
                            migrationActive = false;
                            migrationError = true;
                        }

                        logger.Info("Migration setting network requirements..");

                        //m?.Close();

                        migrationActive = false;

                        // Only restart app upon successfull completion otherwise show error and shut down
                        if (migrationError)
                        {
                            ServerShutdown?.Invoke(null,
                                new ReasonedEventArgs
                                {
                                    Reason =
                                        $"{Resources.Migration_LoadError} failed to migrate successfully and shutting down application."
                                });
                            return;
                        }
                        WaitForMigrationThenRestart();

                        return;
                    }
                    disabledSave = false;

                    if (Directory.Exists(BaseImagesPath) && string.IsNullOrEmpty(ImagesPath))
                    {
                        ImagesPath = BaseImagesPath;
                    }
                    if (string.IsNullOrEmpty(ImagesPath))
                        ImagesPath = DefaultImagePath;
                    SaveSettings();

                    // Just in case start once for new configurations as admin to set permissions if needed
                    if (startedWithFreshConfig && !Utils.IsAdministrator() && !Utils.IsRunningOnMono())
                    {
                        logger.Info("User has fresh config, restarting once as admin.");
                        Utils.RestartAsAdmin();
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    migrationError = true;
                    migrationActive = false;

                    logger.Error(ex, $"Error occured during LoadSettings (UnauthorizedAccessException): {ex}");
                    var message = "Failed to set folder permissions, do you want to automatically retry as admin?";

                    if (!Utils.IsAdministrator())
                        message = "Failed to set folder permissions, do you want to try and reset folder permissions?";
                    bool res = Utils.ShowYesNo("Failed to set folder permissions", message);
                    switch (res)
                    {
                        case true:
                            // gonna try grant access again in advance
                            try
                            {
                                //TODO add MessageBox If Failed
                                if (!Utils.GrantAccess(ApplicationPath))
                                {
                                    logger.Error("Unable to gain permissions for program data. Try running as admin.");
                                }
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                            Utils.RestartAsAdmin();
                            break;
                        case false:
                            ServerShutdown?.Invoke(null, new ReasonedEventArgs { Reason="Failed to set folder permissions" } );
                            Environment.Exit(0);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                migrationError = true;
                migrationActive = false;
                Utils.ShowErrorMessage(Resources.Migration_LoadError, $"{Resources.Migration_LoadError} {e.Message}");
                logger.Error(e);
                ServerShutdown?.Invoke(null, new ReasonedEventArgs {Exception = e});
            }
        }



        public class FileEventArgs : EventArgs { public string FileName { get; set; } }


        public static event EventHandler<FileEventArgs> LocateFile;

        public static void LoadLegacySettingsFromFile(bool locateAutomatically)
        {
            try
            {
                string configFile = string.Empty;

                // Try to locate old config
                if (locateAutomatically)
                {
                    // First try to locate it from old JMM Server installer entry
                    string jmmServerInstallLocation = null;
                    if (!Utils.IsRunningOnMono()) jmmServerInstallLocation = (string) Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{898530ED-CFC7-4744-B2B8-A8D98A2FA06C}_is1",
                        "InstallLocation", null);

                    if (!string.IsNullOrEmpty(jmmServerInstallLocation))
                    {
                        configFile = Path.Combine(jmmServerInstallLocation, "JMMServer.exe.config");
                    }

                    if (!File.Exists(configFile))
                        configFile = @"C:\Program Files (x86)\JMM\JMM Server\JMMServer.exe.config";
                    if (!File.Exists(configFile))
                        configFile = @"C:\Program Files (x86)\JMM Server\JMMServer.exe.config";
                    if (!File.Exists(configFile))
                        configFile = "JMMServer.exe.config";
                    if (!File.Exists(configFile))
                        configFile = "old.config";
                }

                // Ask user if they want to find config manually
                if (!File.Exists(configFile))
                    configFile = LocateLegacyConfigFile();

                if (configFile.ToLower().Contains("settings.json"))
                {
                    appSettings =
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(configFile));
                }
                else
                {
                    var col = GetNameValueCollectionSection("appSettings", configFile);

                    // if old settings found store and replace with new ShokoServer naming if needed
                    // else fallback on current one we have
                    if (col.Count > 0)
                    {
                        // Store default settings for later use
                        var colDefault = ConfigurationManager.AppSettings;
                        var appSettingDefault = colDefault.AllKeys.ToDictionary(a => a, a => colDefault[a]);

                        appSettings.Clear();
                        Dictionary<string, string> appSettingsBeforeRename = col.AllKeys.ToDictionary(a => a,
                            a => col[a]);

                        foreach (var setting in appSettingsBeforeRename)
                        {
                            if (!string.IsNullOrEmpty(setting.Value))
                            {
                                string newKey = setting.Key.Replace("JMMServer", "ShokoServer");
                                appSettings.Add(newKey, setting.Value);
                            }
                        }

                        // Check if we missed any setting keys and re-add from stock one
                        foreach (var setting in appSettingDefault)
                        {
                            if (!string.IsNullOrEmpty(setting.Value))
                            {
                                if (!appSettings.ContainsKey(setting.Key))
                                {
                                    string newKey = setting.Key.Replace("JMMServer", "ShokoServer");
                                    appSettings.Add(newKey, setting.Value);
                                }
                            }
                        }
                    }
                    else
                    {
                        col = ConfigurationManager.AppSettings;
                        appSettings = col.AllKeys.ToDictionary(a => a, a => col[a]);
                    }
                }
            }
            catch (Exception ex)
            {
                // Load default settings as otherwise will fail to start entirely
                var col = ConfigurationManager.AppSettings;
                appSettings = col.AllKeys.ToDictionary(a => a, a => col[a]);
                logger.Error($"Error occured during LoadSettingsManuallyFromFile: {ex.Message}");
                logger.Fatal(ex.StackTrace);
            }
        }

        public static string LocateLegacyConfigFile()
        {
            string configPath = string.Empty;
            bool res = Utils.ShowYesNo(Resources.LocateSettingsFile, Resources.LocateSettingsFileDialog);

            if (!res) return configPath;

            FileEventArgs fea = new FileEventArgs();
            LocateFile?.Invoke(null, fea);
            if (!string.IsNullOrEmpty(fea.FileName))
                configPath = fea.FileName;

            return configPath;
        }

        public static bool HasAllNecessaryFields(Dictionary<string, string> settings)
        {
            // More could be added, but in every case I've seen of a wtf, these were missing
            if (settings?.ContainsKey("AniDB_Username") == true && settings.ContainsKey("AniDB_Password"))
                return true;

            return false;
        }

        public static void LoadSettings()
        {
            LoadSettingsFromFile(string.Empty);
        }

        public static event EventHandler MigrationStarted;
        public static event EventHandler<RunWorkerCompletedEventArgs> MigrationEnded;

        private static void MigrationIndicatorForm()
        {
            // Configure a BackgroundWorker to perform your long running operation.
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += Bg_migrationStart;
            bg.RunWorkerCompleted += Bg_migrationFinished;

            // Start the worker.
            bg.RunWorkerAsync();

            MigrationStarted?.Invoke(bg, null);
        }

        private static void Bg_migrationStart(object sender, DoWorkEventArgs e)
        {
            while (migrationActive && !migrationError)
            {
            }
        }

        private static void Bg_migrationFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            // Retrieve the result pass from bg_DoWork() if any.
            // Note, you may need to cast it to the desired data type.
            //object result = e.Result;

            MigrationEnded?.Invoke(sender, e);
        }

        private static void WaitForMigrationThenRestart()
        {
            string exePath = Assembly.GetEntryAssembly().FullName;//System.Windows.Forms.Application.ExecutablePath;
            logger.Log(LogLevel.Info, $"WaitForMigrationThenRestart executable path: {exePath}");

            try
            {
                if (File.Exists(exePath))
                {
                    ProcessStartInfo Info = new ProcessStartInfo
                    {
                        Arguments = "/C ping 127.0.0.1 -n 3 && \"" + exePath + "\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        FileName = @"C:\windows\system32\cmd.exe"
                    };
                    Process.Start(Info);
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"Error occured during WaitForMigrationThenRestart: {ex.Message}");
            }
        }

        private static bool disabledSave;

        public static void SaveSettings()
        {
            if (disabledSave)
                return;
            lock (appSettings)
            {
                if (appSettings.Count == 1)
                    return; //Somehow debugging may fuck up the settings so this shit will eject
                string path = Path.Combine(ApplicationPath, "settings.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(appSettings, Formatting.Indented));
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
            set => Set("AnimeXmlDirectory", value);
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
            set => Set("MyListDirectory", value);
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
            set => Set("MySqliteDirectory", value);
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
            set => Set("DatabaseBackupDirectory", value);
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
            set => Set("JMMServerPort", value);
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
            set => Set("PluginAutoWatchThreshold", value);
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
            set => Set("PlexThumbnailAspect", value);
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
            set => Set("Culture", value);
        }


        #region LogRotator

        public static bool RotateLogs
        {
            get
            {
                bool val = true;
                if (!string.IsNullOrEmpty(Get("RotateLogs")))
                {
                    bool.TryParse(Get("RotateLogs"), out val);
                }
                else
                {
                    RotateLogs = true;
                }
                return val;
            }
            set => Set("RotateLogs", value.ToString());
        }

        public static bool RotateLogs_Zip
        {
            get
            {
                bool val = true;
                if (!string.IsNullOrEmpty(Get("RotateLogs_Zip")))
                {
                    bool.TryParse(Get("RotateLogs_Zip"), out val);
                }
                else
                {
                    RotateLogs = true;
                }
                return val;
            }
            set => Set("RotateLogs_Zip", value.ToString());
        }

        public static bool RotateLogs_Delete
        {
            get
            {
                bool val = true;
                if (!string.IsNullOrEmpty(Get("RotateLogs_Delete")))
                {
                    bool.TryParse(Get("RotateLogs_Delete"), out val);
                }
                else
                {
                    RotateLogs = true;
                }
                return val;
            }
            set => Set("RotateLogs_Delete", value.ToString());
        }

        public static string RotateLogs_Delete_Days
        {
            get => Get("RotateLogs_Delete_Days");
            set => Set("RotateLogs_Delete_Days", value);
        }

        #endregion

        #region WebUI

        /// <summary>
        /// Store json settings inside string
        /// </summary>
        public static string WebUI_Settings
        {
            get => Get("WebUI_Settings");
            set => Set("WebUI_Settings", value);
        }

        /// <summary>
        /// FirstRun idicates if DB was configured or not, as it needed as backend for user authentication
        /// </summary>
        public static bool FirstRun
        {
            get
            {
                bool val = true;
                if (!string.IsNullOrEmpty(Get("FirstRun")))
                {
                    bool.TryParse(Get("FirstRun"), out val);
                }
                else
                {
                    FirstRun = true;
                }
                return val;
            }
            set => Set("FirstRun", value.ToString());
        }

        #endregion

        #region Database

        public static string DefaultUserUsername { get; set; } = "Default";
        public static string DefaultUserPassword { get; set; } = string.Empty;

        public static string DatabaseType
        {
            get => Get("DatabaseType");
            set => Set("DatabaseType", value);
        }

        public static string DatabaseServer
        {
            get => Get("SQLServer_DatabaseServer");
            set => Set("SQLServer_DatabaseServer", value);
        }

        public static string DatabaseName
        {
            get => Get("SQLServer_DatabaseName");
            set => Set("SQLServer_DatabaseName", value);
        }

        public static string DatabaseUsername
        {
            get => Get("SQLServer_Username");
            set => Set("SQLServer_Username", value);
        }

        public static string DatabasePassword
        {
            get => Get("SQLServer_Password");
            set => Set("SQLServer_Password", value);
        }

        public static string DatabaseFile
        {
            get => Get("SQLite_DatabaseFile");
            set => Set("SQLite_DatabaseFile", value);
        }

        public static string MySQL_Hostname
        {
            get => Get("MySQL_Hostname");
            set => Set("MySQL_Hostname", value);
        }

        public static string MySQL_SchemaName
        {
            get => Get("MySQL_SchemaName");
            set => Set("MySQL_SchemaName", value);
        }

        public static string MySQL_Username
        {
            get => Get("MySQL_Username");
            set => Set("MySQL_Username", value);
        }

        public static string MySQL_Password
        {
            get => Get("MySQL_Password");
            set => Set("MySQL_Password", value);
        }

        #endregion

        #region AniDB

        public static string AniDB_Username
        {
            get => Get("AniDB_Username");
            set => Set("AniDB_Username", value);
        }

        public static string AniDB_Password
        {
            get => Get("AniDB_Password");
            set => Set("AniDB_Password", value);
        }

        public static string AniDB_ServerAddress
        {
            get => Get("AniDB_ServerAddress");
            set => Set("AniDB_ServerAddress", value);
        }

        public static string AniDB_ServerPort
        {
            get => Get("AniDB_ServerPort");
            set => Set("AniDB_ServerPort", value);
        }

        public static string AniDB_ClientPort
        {
            get => Get("AniDB_ClientPort");
            set => Set("AniDB_ClientPort", value);
        }

        public static string AniDB_AVDumpKey
        {
            get => Get("AniDB_AVDumpKey");
            set => Set("AniDB_AVDumpKey", value);
        }

        public static string AniDB_AVDumpClientPort
        {
            get => Get("AniDB_AVDumpClientPort");
            set => Set("AniDB_AVDumpClientPort", value);
        }

        public static bool AniDB_DownloadRelatedAnime
        {
            get
            {
                bool.TryParse(Get("AniDB_DownloadRelatedAnime"), out bool download);
                return download;
            }
            set => Set("AniDB_DownloadRelatedAnime", value.ToString());
        }

        public static bool AniDB_DownloadSimilarAnime
        {
            get
            {
                bool.TryParse(Get("AniDB_DownloadSimilarAnime"), out bool download);
                return download;
            }
            set => Set("AniDB_DownloadSimilarAnime", value.ToString());
        }

        public static bool AniDB_DownloadReviews
        {
            get
            {
                bool.TryParse(Get("AniDB_DownloadReviews"), out bool download);
                return download;
            }
            set => Set("AniDB_DownloadReviews", value.ToString());
        }

        public static bool AniDB_DownloadReleaseGroups
        {
            get
            {
                bool.TryParse(Get("AniDB_DownloadReleaseGroups"), out bool download);
                return download;
            }
            set => Set("AniDB_DownloadReleaseGroups", value.ToString());
        }

        public static bool AniDB_MyList_AddFiles
        {
            get
            {
                bool.TryParse(Get("AniDB_MyList_AddFiles"), out bool val);
                return val;
            }
            set => Set("AniDB_MyList_AddFiles", value.ToString());
        }

        public static AniDBFile_State AniDB_MyList_StorageState
        {
            get
            {
                int.TryParse(Get("AniDB_MyList_StorageState"), out int val);

                return (AniDBFile_State) val;
            }
            set => Set("AniDB_MyList_StorageState", ((int) value).ToString());
        }

        public static AniDBFileDeleteType AniDB_MyList_DeleteType
        {
            get
            {
                int.TryParse(Get("AniDB_MyList_DeleteType"), out int val);

                return (AniDBFileDeleteType) val;
            }
            set => Set("AniDB_MyList_DeleteType", ((int) value).ToString());
        }

        public static bool AniDB_MyList_ReadUnwatched
        {
            get
            {
                bool.TryParse(Get("AniDB_MyList_ReadUnwatched"), out bool val);
                return val;
            }
            set => Set("AniDB_MyList_ReadUnwatched", value.ToString());
        }

        public static bool AniDB_MyList_ReadWatched
        {
            get
            {
                bool.TryParse(Get("AniDB_MyList_ReadWatched"), out bool val);
                return val;
            }
            set => Set("AniDB_MyList_ReadWatched", value.ToString());
        }

        public static bool AniDB_MyList_SetWatched
        {
            get
            {
                bool.TryParse(Get("AniDB_MyList_SetWatched"), out bool val);
                return val;
            }
            set => Set("AniDB_MyList_SetWatched", value.ToString());
        }

        public static bool AniDB_MyList_SetUnwatched
        {
            get
            {
                bool.TryParse(Get("AniDB_MyList_SetUnwatched"), out bool val);
                return val;
            }
            set => Set("AniDB_MyList_SetUnwatched", value.ToString());
        }

        public static ScheduledUpdateFrequency AniDB_MyList_UpdateFrequency
        {
            get
            {
                if (int.TryParse(Get("AniDB_MyList_UpdateFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Never; // default value
            }
            set => Set("AniDB_MyList_UpdateFrequency", ((int) value).ToString());
        }

        public static ScheduledUpdateFrequency AniDB_Calendar_UpdateFrequency
        {
            get
            {
                if (int.TryParse(Get("AniDB_Calendar_UpdateFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set => Set("AniDB_Calendar_UpdateFrequency", ((int) value).ToString());
        }

        public static ScheduledUpdateFrequency AniDB_Anime_UpdateFrequency
        {
            get
            {
                if (int.TryParse(Get("AniDB_Anime_UpdateFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set => Set("AniDB_Anime_UpdateFrequency", ((int) value).ToString());
        }

        public static ScheduledUpdateFrequency AniDB_MyListStats_UpdateFrequency
        {
            get
            {
                if (int.TryParse(Get("AniDB_MyListStats_UpdateFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Never; // default value
            }
            set => Set("AniDB_MyListStats_UpdateFrequency", ((int) value).ToString());
        }

        public static ScheduledUpdateFrequency AniDB_File_UpdateFrequency
        {
            get
            {
                if (int.TryParse(Get("AniDB_File_UpdateFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Daily; // default value
            }
            set => Set("AniDB_File_UpdateFrequency", ((int) value).ToString());
        }

        public static bool AniDB_DownloadCharacters
        {
            get
            {
                if (!bool.TryParse(Get("AniDB_DownloadCharacters"), out bool val))
                    val = true; // default
                return val;
            }
            set => Set("AniDB_DownloadCharacters", value.ToString());
        }

        public static bool AniDB_DownloadCreators
        {
            get
            {
                if (!bool.TryParse(Get("AniDB_DownloadCreators"), out bool val))
                    val = true; // default
                return val;
            }
            set => Set("AniDB_DownloadCreators", value.ToString());
        }

        #endregion

        #region Web Cache

        public static string WebCache_Address
        {
            get => Get("WebCache_Address");
            set => Set("WebCache_Address", value);
        }

        public static bool WebCache_Anonymous
        {
            get
            {
                bool.TryParse(Get("WebCache_Anonymous"), out bool val);
                return val;
            }
            set => Set("WebCache_Anonymous", value.ToString());
        }

        public static bool WebCache_XRefFileEpisode_Get
        {
            get
            {
                bool.TryParse(Get("WebCache_XRefFileEpisode_Get"), out bool usecache);
                return usecache;
            }
            set => Set("WebCache_XRefFileEpisode_Get", value.ToString());
        }

        public static bool WebCache_XRefFileEpisode_Send
        {
            get
            {
                bool.TryParse(Get("WebCache_XRefFileEpisode_Send"), out bool usecache);
                return usecache;
            }
            set => Set("WebCache_XRefFileEpisode_Send", value.ToString());
        }

        public static bool WebCache_TvDB_Get
        {
            get
            {
                if (bool.TryParse(Get("WebCache_TvDB_Get"), out bool usecache))
                    return usecache;
                return true; // default
            }
            set => Set("WebCache_TvDB_Get", value.ToString());
        }

        public static bool WebCache_TvDB_Send
        {
            get
            {
                if (bool.TryParse(Get("WebCache_TvDB_Send"), out bool usecache))
                    return usecache;
                return true; // default
            }
            set => Set("WebCache_TvDB_Send", value.ToString());
        }

        public static bool WebCache_Trakt_Get
        {
            get
            {
                if (bool.TryParse(Get("WebCache_Trakt_Get"), out bool usecache))
                    return usecache;
                return true; // default
            }
            set => Set("WebCache_Trakt_Get", value.ToString());
        }

        public static bool WebCache_Trakt_Send
        {
            get
            {
                if (bool.TryParse(Get("WebCache_Trakt_Send"), out bool usecache))
                    return usecache;
                return true; // default
            }
            set => Set("WebCache_Trakt_Send", value.ToString());
        }

        public static bool WebCache_UserInfo
        {
            get
            {
                if (bool.TryParse(Get("WebCache_UserInfo"), out bool usecache))
                    return usecache;
                return true; // default
            }
            set => Set("WebCache_UserInfo", value.ToString());
        }

        #endregion

        #region TvDB

        public static bool TvDB_AutoLink
        {
            get
            {
                bool.TryParse(Get("TvDB_AutoLink"), out bool val);
                return val;
            }
            set => Set("TvDB_AutoLink", value.ToString());
        }

        public static bool TvDB_AutoFanart
        {
            get
            {
                bool.TryParse(Get("TvDB_AutoFanart"), out bool val);
                return val;
            }
            set => Set("TvDB_AutoFanart", value.ToString());
        }

        public static int TvDB_AutoFanartAmount
        {
            get
            {
                int.TryParse(Get("TvDB_AutoFanartAmount"), out int val);
                return val;
            }
            set => Set("TvDB_AutoFanartAmount", value.ToString());
        }

        public static bool TvDB_AutoWideBanners
        {
            get
            {
                bool.TryParse(Get("TvDB_AutoWideBanners"), out bool val);
                return val;
            }
            set => Set("TvDB_AutoWideBanners", value.ToString());
        }

        public static int TvDB_AutoWideBannersAmount
        {
            get
            {
                if (!int.TryParse(Get("TvDB_AutoWideBannersAmount"), out int val))
                    val = 10; // default
                return val;
            }
            set => Set("TvDB_AutoWideBannersAmount", value.ToString());
        }

        public static bool TvDB_AutoPosters
        {
            get
            {
                bool.TryParse(Get("TvDB_AutoPosters"), out bool val);
                return val;
            }
            set => Set("TvDB_AutoPosters", value.ToString());
        }

        public static int TvDB_AutoPostersAmount
        {
            get
            {
                if (!int.TryParse(Get("TvDB_AutoPostersAmount"), out int val))
                    val = 10; // default
                return val;
            }
            set => Set("TvDB_AutoPostersAmount", value.ToString());
        }

        public static ScheduledUpdateFrequency TvDB_UpdateFrequency
        {
            get
            {
                if (int.TryParse(Get("TvDB_UpdateFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set => Set("TvDB_UpdateFrequency", ((int) value).ToString());
        }

        public static string TvDB_Language
        {
            get
            {
                string language = Get("TvDB_Language");
                return string.IsNullOrEmpty(language)
                    ? "en"
                    : language;
            }
            set => Set("TvDB_Language", value);
        }

        #endregion

        #region MovieDB

        public static bool MovieDB_AutoFanart
        {
            get
            {
                bool.TryParse(Get("MovieDB_AutoFanart"), out bool val);
                return val;
            }
            set => Set("MovieDB_AutoFanart", value.ToString());
        }

        public static int MovieDB_AutoFanartAmount
        {
            get
            {
                int.TryParse(Get("MovieDB_AutoFanartAmount"), out int val);
                return val;
            }
            set => Set("MovieDB_AutoFanartAmount", value.ToString());
        }

        public static bool MovieDB_AutoPosters
        {
            get
            {
                bool.TryParse(Get("MovieDB_AutoPosters"), out bool val);
                return val;
            }
            set => Set("MovieDB_AutoPosters", value.ToString());
        }

        public static int MovieDB_AutoPostersAmount
        {
            get
            {
                if (!int.TryParse(Get("MovieDB_AutoPostersAmount"), out int val))
                    val = 10; // default
                return val;
            }
            set => Set("MovieDB_AutoPostersAmount", value.ToString());
        }

        #endregion

        #region Import Settings

        public static string VideoExtensions
        {
            get => Get("VideoExtensions");
            set => Set("VideoExtensions", value);
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
            set => Set("DefaultSeriesLanguage", ((int) value).ToString());
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
            set => Set("DefaultEpisodeLanguage", ((int) value).ToString());
        }

        public static bool RunImportOnStart
        {
            get
            {
                bool.TryParse(Get("RunImportOnStart"), out bool val);
                return val;
            }
            set => Set("RunImportOnStart", value.ToString());
        }

        public static bool ScanDropFoldersOnStart
        {
            get
            {
                bool.TryParse(Get("ScanDropFoldersOnStart"), out bool val);
                return val;
            }
            set => Set("ScanDropFoldersOnStart", value.ToString());
        }

        public static bool Hash_CRC32
        {
            get
            {
                bool.TryParse(Get("Hash_CRC32"), out bool bval);
                return bval;
            }
            set => Set("Hash_CRC32", value.ToString());
        }

        public static bool Hash_MD5
        {
            get
            {
                bool.TryParse(Get("Hash_MD5"), out bool bval);
                return bval;
            }
            set => Set("Hash_MD5", value.ToString());
        }

        public static bool ExperimentalUPnP
        {
            get
            {
                bool.TryParse(Get("ExperimentalUPnP"), out bool bval);
                return bval;
            }
            set => Set("ExperimentalUPnP", value.ToString());
        }

        public static bool Hash_SHA1
        {
            get
            {
                bool.TryParse(Get("Hash_SHA1"), out bool bval);
                return bval;
            }
            set => Set("Hash_SHA1", value.ToString());
        }

        public static bool Import_UseExistingFileWatchedStatus
        {
            get
            {
                bool.TryParse(Get("Import_UseExistingFileWatchedStatus"), out bool bval);
                return bval;
            }
            set => Set("Import_UseExistingFileWatchedStatus", value.ToString());
        }

        #endregion

        public static bool AutoGroupSeries
        {
            get
            {
                bool.TryParse(Get("AutoGroupSeries"), out bool val);
                return val;
            }
            set => Set("AutoGroupSeries", value.ToString());
        }

        public static string AutoGroupSeriesRelationExclusions
        {
            get
            {
                string val = null;
                try
                {
                    val = Get("AutoGroupSeriesRelationExclusions");
                }
                catch
                {
                    // ignored
                }
                return val ?? "same setting|character";
            }
            set => Set("AutoGroupSeriesRelationExclusions", value);
        }

        public static bool AutoGroupSeriesUseScoreAlgorithm
        {
            get
            {
                bool.TryParse(Get("AutoGroupSeriesUseScoreAlgorithm"), out bool val);
                return val;
            }
            set => Set("AutoGroupSeriesUseScoreAlgorithm", value.ToString());
        }

        public static bool FileQualityFilterEnabled
        {
            get
            {
                bool.TryParse(Get("FileQualityFilterEnabled"), out bool val);
                return val;
            }
            set => Set("FileQualityFilterEnabled", value.ToString());
        }

        public static string FileQualityFilterPreferences
        {
            get
            {
                string val = null;
                try
                {
                    val = Get("FileQualityFilterPreferences");
                }
                catch
                {
                    // ignored
                }
                return val ?? JsonConvert.SerializeObject(FileQualityFilter.Settings, Formatting.None, new StringEnumConverter());
            }
            set
            {
                try
                {
                    FileQualityPreferences prefs = JsonConvert.DeserializeObject<FileQualityPreferences>(
                        value, new StringEnumConverter());
                    FileQualityFilter.Settings = prefs;
                    Set("FileQualityFilterPreferences", value);
                }
                catch
                {
                    logger.Error("Error Deserializing json into FileQualityPreferences. json was :" + value);
                }

            }
        }

        public static string LanguagePreference
        {
            get => Get("LanguagePreference");
            set => Set("LanguagePreference", value);
        }

        public static string EpisodeLanguagePreference
        {
            get => Get("EpisodeLanguagePreference");
            set => Set("EpisodeLanguagePreference", value);
        }

        public static bool LanguageUseSynonyms
        {
            get
            {
                bool.TryParse(Get("LanguageUseSynonyms"), out bool val);
                return val;
            }
            set => Set("LanguageUseSynonyms", value.ToString());
        }

        public static int CloudWatcherTime
        {
            get
            {
                int.TryParse(Get("CloudWatcherTime"), out int val);
                if (val == 0)
                    val = 3;
                return val;
            }
            set => Set("CloudWatcherTime", value.ToString());
        }

        public static DataSourceType EpisodeTitleSource
        {
            get
            {
                int.TryParse(Get("EpisodeTitleSource"), out int val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                return (DataSourceType) val;
            }
            set => Set("EpisodeTitleSource", ((int) value).ToString());
        }

        public static DataSourceType SeriesDescriptionSource
        {
            get
            {
                int.TryParse(Get("SeriesDescriptionSource"), out int val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                return (DataSourceType) val;
            }
            set => Set("SeriesDescriptionSource", ((int) value).ToString());
        }

        public static DataSourceType SeriesNameSource
        {
            get
            {
                int.TryParse(Get("SeriesNameSource"), out int val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                return (DataSourceType) val;
            }
            set => Set("SeriesNameSource", ((int) value).ToString());
        }

        public static string ImagesPath
        {
            get => Get("ImagesPath");
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
                    bool.TryParse(basePath, out bool val);
                    return val;
                }
                return true;
            }
        }

        public static string VLCLocation
        {
            get => Get("VLCLocation");
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
                bool.TryParse(Get("MinimizeOnStartup"), out bool val);
                return val;
            }
            set => Set("MinimizeOnStartup", value.ToString());
        }

        #region Trakt

        public static bool Trakt_IsEnabled
        {
            get
            {
                if (!bool.TryParse(Get("Trakt_IsEnabled"), out bool val))
                    val = true;
                return val;
            }
            set => Set("Trakt_IsEnabled", value.ToString());
        }

        public static string Trakt_PIN { get; set; }

        public static string Trakt_AuthToken
        {
            get => Get("Trakt_AuthToken");
            set => Set("Trakt_AuthToken", value);
        }

        public static string Trakt_RefreshToken
        {
            get => Get("Trakt_RefreshToken");
            set => Set("Trakt_RefreshToken", value);
        }

        public static string Trakt_TokenExpirationDate
        {
            get => Get("Trakt_TokenExpirationDate");
            set => Set("Trakt_TokenExpirationDate", value);
        }

        public static ScheduledUpdateFrequency Trakt_UpdateFrequency
        {
            get
            {
                if (int.TryParse(Get("Trakt_UpdateFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Daily; // default value
            }
            set => Set("Trakt_UpdateFrequency", ((int) value).ToString());
        }

        public static ScheduledUpdateFrequency Trakt_SyncFrequency
        {
            get
            {
                if (int.TryParse(Get("Trakt_SyncFrequency"), out int val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Never; // default value
            }
            set => Set("Trakt_SyncFrequency", ((int) value).ToString());
        }

        #endregion

        public static string UpdateChannel
        {
            get
            {
                string val = Get("UpdateChannel");
                if (string.IsNullOrEmpty(val))
                {
                    // default value
                    val = "Stable";
                    Set("UpdateChannel", val);
                }
                return val;
            }
            set => Set("UpdateChannel", value);
        }

        public static string WebCacheAuthKey
        {
            get => Get("WebCacheAuthKey");
            set => Set("WebCacheAuthKey", value);
        }

        #region plex

        //plex
        public static int[] Plex_Libraries
        {
            get
            {
                string values = Get(nameof(Plex_Libraries));
                if (String.IsNullOrEmpty(values)) return new int[0];
                return values.Split(',').Select(int.Parse).ToArray();
            }
            set => Set(nameof(Plex_Libraries), string.Join(",", value));
        }

        public static string Plex_Token
        {
            get => Get(nameof(Plex_Token));
            set => Set(nameof(Plex_Token), value);
        }

        public static string Plex_Server
        {
            get => Get(nameof(Plex_Server));
            set => Set(nameof(Plex_Server), value);
        }

        #endregion

        public static int Linux_UID
        {
            get
            {
                if (!Int32.TryParse(Get(nameof(Linux_UID)), out int val)) return -1;
                return val;
            }
            set { Set(nameof(Linux_UID), value.ToString()); }
        }

        public static int Linux_GID
        {
            get
            {
                if (!Int32.TryParse(Get(nameof(Linux_GID)), out int val)) return -1;
                return val;
            }
            set { Set(nameof(Linux_GID), value.ToString()); }
        }

        public static int Linux_Permission
        {
            get { return Convert.ToInt32(Get(nameof(Linux_Permission)), 8); }
            set { Set(nameof(Linux_Permission), Convert.ToString(value, 8)); }
        }

        public static int AniDB_MaxRelationDepth
        {
            get
            {
                if (!Int32.TryParse(Get(nameof(AniDB_MaxRelationDepth)), out int val)) return 3;
                return val;
            }
            set { Set(nameof(AniDB_MaxRelationDepth), value.ToString()); }
        }

        public static CL_ServerSettings ToContract()
        {
            CL_ServerSettings contract = new CL_ServerSettings
            {
                AniDB_Username = AniDB_Username,
                AniDB_Password = AniDB_Password,
                AniDB_ServerAddress = AniDB_ServerAddress,
                AniDB_ServerPort = AniDB_ServerPort,
                AniDB_ClientPort = AniDB_ClientPort,
                AniDB_AVDumpClientPort = AniDB_AVDumpClientPort,
                AniDB_AVDumpKey = AniDB_AVDumpKey,

                AniDB_DownloadRelatedAnime = AniDB_DownloadRelatedAnime,
                AniDB_DownloadSimilarAnime = AniDB_DownloadSimilarAnime,
                AniDB_DownloadReviews = AniDB_DownloadReviews,
                AniDB_DownloadReleaseGroups = AniDB_DownloadReleaseGroups,

                AniDB_MyList_AddFiles = AniDB_MyList_AddFiles,
                AniDB_MyList_StorageState = (int)AniDB_MyList_StorageState,
                AniDB_MyList_DeleteType = (int)AniDB_MyList_DeleteType,
                AniDB_MyList_ReadWatched = AniDB_MyList_ReadWatched,
                AniDB_MyList_ReadUnwatched = AniDB_MyList_ReadUnwatched,
                AniDB_MyList_SetWatched = AniDB_MyList_SetWatched,
                AniDB_MyList_SetUnwatched = AniDB_MyList_SetUnwatched,

                AniDB_MyList_UpdateFrequency = (int)AniDB_MyList_UpdateFrequency,
                AniDB_Calendar_UpdateFrequency = (int)AniDB_Calendar_UpdateFrequency,
                AniDB_Anime_UpdateFrequency = (int)AniDB_Anime_UpdateFrequency,
                AniDB_MyListStats_UpdateFrequency = (int)AniDB_MyListStats_UpdateFrequency,
                AniDB_File_UpdateFrequency = (int)AniDB_File_UpdateFrequency,

                AniDB_DownloadCharacters = AniDB_DownloadCharacters,
                AniDB_DownloadCreators = AniDB_DownloadCreators,
                AniDB_MaxRelationDepth = AniDB_MaxRelationDepth,

                // Web Cache
                WebCache_Address = WebCache_Address,
                WebCache_Anonymous = WebCache_Anonymous,
                WebCache_XRefFileEpisode_Get = WebCache_XRefFileEpisode_Get,
                WebCache_XRefFileEpisode_Send = WebCache_XRefFileEpisode_Send,
                WebCache_TvDB_Get = WebCache_TvDB_Get,
                WebCache_TvDB_Send = WebCache_TvDB_Send,
                WebCache_Trakt_Get = WebCache_Trakt_Get,
                WebCache_Trakt_Send = WebCache_Trakt_Send,
                WebCache_UserInfo = WebCache_UserInfo,

                // TvDB
                TvDB_AutoLink = TvDB_AutoLink,
                TvDB_AutoFanart = TvDB_AutoFanart,
                TvDB_AutoFanartAmount = TvDB_AutoFanartAmount,
                TvDB_AutoPosters = TvDB_AutoPosters,
                TvDB_AutoPostersAmount = TvDB_AutoPostersAmount,
                TvDB_AutoWideBanners = TvDB_AutoWideBanners,
                TvDB_AutoWideBannersAmount = TvDB_AutoWideBannersAmount,
                TvDB_UpdateFrequency = (int)TvDB_UpdateFrequency,
                TvDB_Language = TvDB_Language,

                // MovieDB
                MovieDB_AutoFanart = MovieDB_AutoFanart,
                MovieDB_AutoFanartAmount = MovieDB_AutoFanartAmount,
                MovieDB_AutoPosters = MovieDB_AutoPosters,
                MovieDB_AutoPostersAmount = MovieDB_AutoPostersAmount,

                // Import settings
                VideoExtensions = VideoExtensions,
                AutoGroupSeries = AutoGroupSeries,
                AutoGroupSeriesUseScoreAlgorithm = AutoGroupSeriesUseScoreAlgorithm,
                AutoGroupSeriesRelationExclusions = AutoGroupSeriesRelationExclusions,
                FileQualityFilterEnabled = FileQualityFilterEnabled,
                FileQualityFilterPreferences = FileQualityFilterPreferences,
                Import_UseExistingFileWatchedStatus = Import_UseExistingFileWatchedStatus,
                RunImportOnStart = RunImportOnStart,
                ScanDropFoldersOnStart = ScanDropFoldersOnStart,
                Hash_CRC32 = Hash_CRC32,
                Hash_MD5 = Hash_MD5,
                Hash_SHA1 = Hash_SHA1,

                // Language
                LanguagePreference = LanguagePreference,
                LanguageUseSynonyms = LanguageUseSynonyms,
                EpisodeTitleSource = (int)EpisodeTitleSource,
                SeriesDescriptionSource = (int)SeriesDescriptionSource,
                SeriesNameSource = (int)SeriesNameSource,

                // trakt
                Trakt_IsEnabled = Trakt_IsEnabled,
                Trakt_AuthToken = Trakt_AuthToken,
                Trakt_RefreshToken = Trakt_RefreshToken,
                Trakt_TokenExpirationDate = Trakt_TokenExpirationDate,
                Trakt_UpdateFrequency = (int)Trakt_UpdateFrequency,
                Trakt_SyncFrequency = (int)Trakt_SyncFrequency,

                // LogRotator
                RotateLogs = RotateLogs,
                RotateLogs_Delete = RotateLogs_Delete,
                RotateLogs_Delete_Days = RotateLogs_Delete_Days,
                RotateLogs_Zip = RotateLogs_Zip,

                //WebUI
                WebUI_Settings = WebUI_Settings,

                //Plex
                Plex_Sections = String.Join(",", Plex_Libraries),
                Plex_ServerHost = Plex_Server
            };
            return contract;
        }

        public static void DebugSettingsToLog()
        {
            #region System Info

            logger.Info("-------------------- SYSTEM INFO -----------------------");

            Assembly a = Assembly.GetEntryAssembly();
            try
            {
                if (Utils.GetApplicationVersion(a) != null)
                    logger.Info($"Shoko Server Version: v{Utils.GetApplicationVersion(a)}");
            }
            catch (Exception ex)
            {
                logger.Warn($"Error in log (server version lookup): {ex}");
            }

            try
            {
                if (DatabaseFactory.Instance != null)
                    logger.Info($"Database Version: {DatabaseFactory.Instance.GetDatabaseVersion()}");
            }
            catch (Exception ex)
            {
                // oopps, can't create file
                logger.Warn("Error in log (database version lookup: {0}", ex.Message);
            }

            logger.Info($"Operating System: {Utils.GetOSInfo()}");

            try
            {
                string mediaInfoVersion = "**** MediaInfo - DLL Not found *****";

                string mediaInfoPath = Assembly.GetEntryAssembly().Location;
                FileInfo fi = new FileInfo(mediaInfoPath);
                mediaInfoPath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "MediaInfo.dll");

                if (File.Exists(mediaInfoPath))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(mediaInfoPath);
                    mediaInfoVersion =
                        $"MediaInfo DLL {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({mediaInfoPath})";
                }
                logger.Info(mediaInfoVersion);

                string hasherInfoVersion = "**** Hasher - DLL NOT found *****";

                string fullHasherexepath = Assembly.GetEntryAssembly().Location;
                fi = new FileInfo(fullHasherexepath);
                fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "hasher.dll");

                if (File.Exists(fullHasherexepath))
                    hasherInfoVersion = $"Hasher DLL found at {fullHasherexepath}";
                logger.Info(hasherInfoVersion);
            }
            catch (Exception ex)
            {
                logger.Error("Error in log (hasher / info): {0}", ex.Message);
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
            logger.Info($"{nameof(AniDB_MaxRelationDepth)}: {AniDB_MaxRelationDepth}");


            logger.Info("WebCache_Address: {0}", WebCache_Address);
            logger.Info("WebCache_Anonymous: {0}", WebCache_Anonymous);
            logger.Info("WebCache_XRefFileEpisode_Get: {0}", WebCache_XRefFileEpisode_Get);
            logger.Info("WebCache_XRefFileEpisode_Send: {0}", WebCache_XRefFileEpisode_Send);
            logger.Info("WebCache_TvDB_Get: {0}", WebCache_TvDB_Get);
            logger.Info("WebCache_TvDB_Send: {0}", WebCache_TvDB_Send);

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
            logger.Info("Trakt_RefreshToken: {0}",
                string.IsNullOrEmpty(Trakt_RefreshToken) ? "NOT SET" : "***HIDDEN***");
            logger.Info("Trakt_UpdateFrequency: {0}", Trakt_UpdateFrequency);
            logger.Info("Trakt_SyncFrequency: {0}", Trakt_SyncFrequency);

            logger.Info("AutoGroupSeries: {0}", AutoGroupSeries);
            logger.Info("AutoGroupSeriesRelationExclusions: {0}", AutoGroupSeriesRelationExclusions);
            logger.Info("FileQualityFilterEnabled: {0}", FileQualityFilterEnabled);
            logger.Info("FileQualityFilterPreferences: {0}", FileQualityFilterPreferences);
            logger.Info("LanguagePreference: {0}", LanguagePreference);
            logger.Info("LanguageUseSynonyms: {0}", LanguageUseSynonyms);
            logger.Info("EpisodeTitleSource: {0}", EpisodeTitleSource);
            logger.Info("SeriesDescriptionSource: {0}", SeriesDescriptionSource);
            logger.Info("SeriesNameSource: {0}", SeriesNameSource);
            logger.Info("BaseImagesPath: {0}", BaseImagesPath);
            logger.Info("BaseImagesPathIsDefault: {0}", BaseImagesPathIsDefault);


            logger.Info("-------------------------------------------------------");
        }

        private static NameValueCollection GetNameValueCollectionSection(string section, string filePath)
        {
            string file = filePath;
            XmlDocument xDoc = new XmlDocument();
            NameValueCollection nameValueColl = new NameValueCollection();


            ExeConfigurationFileMap map = new ExeConfigurationFileMap
            {
                ExeConfigFilename = file
            };
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            string xml = config.GetSection(section).SectionInformation.GetRawXml();
            xDoc.LoadXml(xml);
            XmlNode xList = xDoc.ChildNodes[0];
            foreach (XmlNode xNodo in xList)
            {
                try
                {
                    if (xNodo?.Attributes?[0]?.Value == null) continue;
                    nameValueColl.Add(xNodo.Attributes[0].Value, xNodo.Attributes[1].Value);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return nameValueColl;
        }
    }
}
