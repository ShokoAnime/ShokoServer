using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NLog;
using NLog.Targets;
using Shoko.Server.Settings;

namespace Shoko.Server.Server;

public class LogRotator
{
    private static readonly Lazy<LogRotator> _instance = new(() => new LogRotator());
    public static LogRotator Instance => _instance.Value;

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
        var fileTarget = (FileTarget)LogManager.Configuration.FindTargetByName("file");
        return fileTarget == null
            ? string.Empty
            : Path.GetFullPath(fileTarget.FileName.Render(new LogEventInfo { Level = LogLevel.Info }));
    }

    private string GetDirectory()
    {
        return new FileInfo(GetCurrentLogFile()).DirectoryName;
    }

    private List<string> GetFileNames()
    {
        var list = new List<string>();
        var di = new DirectoryInfo(GetDirectory());
        if (!di.Exists)
        {
            di.Create();
        }

        if (di != null)
        {
            foreach (var fi in di.GetFiles())
            {
                list.Add(fi.Name);
            }
        }

        return list;
    }

    private DateTime GetDateTime(string filename)
    {
        filename = filename.Substring(0, filename.Length - 4);
        if (!DateTime.TryParse(filename, out var date))
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
            var compress = new List<string>();
            foreach (var file in GetFileNames())
            {
                if (!file.Contains(".zip"))
                {
                    compress.Add(file);
                }
            }

            //remove current logs file from compress list
            compress.Remove(new FileInfo(GetCurrentLogFile()).Name);

            foreach (var file_ext in compress)
            {
                var file = file_ext.Replace("txt", string.Empty);
                var path = Path.Combine(GetDirectory(), file);

                if (File.Exists(path + "txt"))
                {
                    using (var fs =
                           new FileStream(path + "zip", FileMode.Create))
                    using (var arch = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        arch.CreateEntryFromFile(path + "txt", file_ext);
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
                var now = DateTime.Now;
                var delete = new List<string>();
                if (int.TryParse(ServerSettings.Instance.LogRotator.Delete_Days, out var days))
                {
                    double dec = -1 * days;
                    now = now.AddDays(dec);
                    foreach (var file in GetFileNames())
                    {
                        var lol = GetDateTime(file);
                        if (GetDateTime(file) < now && file.Contains(".zip"))
                        {
                            delete.Add(Path.Combine(GetDirectory(), file));
                        }
                    }
                }

                foreach (var file in delete)
                {
                    var path = Path.Combine(GetDirectory(), file);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
        }
    }
}
