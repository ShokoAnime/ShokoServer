using NLog;
using NLog.Targets;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Shoko.Server.Settings;


namespace Shoko.Server
{
    public class LogRotator
    {
        private static readonly Lazy<LogRotator> _instance = new Lazy<LogRotator>(() => new LogRotator());
        public static LogRotator Instance => _instance.Value;
        public LogRotator()
        {
        }

        public void Start()
        {
            if (ServerSettings.Instance.LogRotator.Enabled)
            {
                Delete_Logs();
                Compress_Logs();
            }
        }

        internal static string GetCurrentLogFile()
        {
            var fileTarget = (FileTarget) LogManager.Configuration.FindTargetByName("file");
            return fileTarget == null
                ? string.Empty
                : Path.GetFullPath(fileTarget.FileName.Render(new LogEventInfo {Level = LogLevel.Info}));
        }

        private string GetDirectory()
        {
            return new FileInfo(GetCurrentLogFile()).DirectoryName;
        }

        private List<string> GetFileNames()
        {
            List<string> list = new List<string>();
            DirectoryInfo di = new DirectoryInfo(GetDirectory());
            if (!di.Exists) di.Create();

            if (di != null)
            {
                foreach (FileInfo fi in di.GetFiles())
                {
                    list.Add(fi.Name);
                }
            }

            return list;
        }

        private DateTime GetDateTime(string filename)
        {
            filename = filename.Substring(0, filename.Length - 4);
            if (!DateTime.TryParse(filename, out DateTime date))
            {
                date = DateTime.Now;
            }
            return date;
        }

        private void Compress_Logs()
        {
            if (ServerSettings.Instance.LogRotator.Zip)
            {
                //compress
                List<string> compress = new List<string>();
                foreach (string file in GetFileNames())
                {
                    if (!file.Contains(".zip"))
                    {
                        compress.Add(file);
                    }
                }

                //remove current logs file from compress list
                compress.Remove(new FileInfo(GetCurrentLogFile()).Name);

                foreach (string file_ext in compress)
                {
                    string file = file_ext.Replace("txt", string.Empty);
                    string path = Path.Combine(GetDirectory(), file);

                    if (File.Exists(path + "txt"))
                    {
                        using (System.IO.FileStream fs =
                            new System.IO.FileStream(@path + "zip", System.IO.FileMode.Create))
                        using (ZipArchive arch = new ZipArchive(fs, ZipArchiveMode.Create))
                        {
                            arch.CreateEntryFromFile(@path + "txt", file_ext);
                        }

                        File.Delete(path + "txt");
                    }
                }
            }
        }

        private void Delete_Logs()
        {
            if (ServerSettings.Instance.LogRotator.Delete)
            {
                if (!string.IsNullOrEmpty(ServerSettings.Instance.LogRotator.Delete_Days))
                {
                    //delete
                    DateTime now = DateTime.Now;
                    List<string> delete = new List<string>();
                    if (int.TryParse(ServerSettings.Instance.LogRotator.Delete_Days, out int days))
                    {
                        double dec = (-1 * days);
                        now = now.AddDays(dec);
                        foreach (string file in GetFileNames())
                        {
                            DateTime lol = GetDateTime(file);
                            if (GetDateTime(file) < now && file.Contains(".zip"))
                            {
                                delete.Add(Path.Combine(GetDirectory(), file));
                            }
                        }
                    }

                    foreach (string file in delete)
                    {
                        string path = Path.Combine(GetDirectory(), file);
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                }
            }
        }
    }
}