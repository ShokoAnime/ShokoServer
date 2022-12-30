using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Timers;
using NLog;
using NLog.Targets;
using Shoko.Server.Settings;

namespace Shoko.Server.Server;

public class LogRotator
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly Timer _timer;
    private readonly BackgroundWorker _worker = new();

    public LogRotator(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
        _timer = new Timer
        {
            AutoReset = true,
            // 86400000 = 24h
            Interval = 86400000
        };
        _timer.Elapsed += TimerElapsed;
        _worker.WorkerReportsProgress = false;
        _worker.WorkerSupportsCancellation = false;
        _worker.DoWork += WorkerDoWork;
    }

    private void TimerElapsed(object sender, ElapsedEventArgs e)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.LogRotator.Enabled) return;

        _worker.RunWorkerAsync();
    }
    
    private void WorkerDoWork(object sender, DoWorkEventArgs e)
    {
        Delete_Logs();
        Compress_Logs();
    }

    public void Start()
    {
        _worker.RunWorkerAsync();
        _timer.Start();
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
        var di = new DirectoryInfo(GetDirectory());
        if (!di.Exists)
        {
            di.Create();
        }

        return di.GetFiles().Select(fi => fi.FullName).ToList();
    }

    private DateTime GetDateTime(string filename)
    {
        filename = Path.GetFileNameWithoutExtension(filename);
        if (!DateTime.TryParse(filename, out var date))
        {
            date = DateTime.Now;
        }

        return date;
    }

    private void Compress_Logs()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.LogRotator.Zip) return;

        //compress
        var compress = GetFileNames().Where(file => !file.EndsWith(".zip")).ToList();

        //remove current logs file from compress list
        compress.Remove(new FileInfo(GetCurrentLogFile()).FullName);

        foreach (var file_ext in compress)
        {
            var file = Path.GetFileNameWithoutExtension(file_ext);
            var path = Path.Combine(GetDirectory(), file);

            if (!File.Exists(file_ext))
                continue;

            using var fs = new FileStream(path + ".zip", FileMode.Create);
            using var arch = new ZipArchive(fs, ZipArchiveMode.Create);
            arch.CreateEntryFromFile(file_ext, Path.GetFileName(file_ext));
            File.Delete(file_ext);
        }
    }

    private void Delete_Logs()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.LogRotator.Delete) return;

        if (string.IsNullOrEmpty(settings.LogRotator.Delete_Days)) return;

        //delete
        var now = DateTime.Now;
        var delete = new List<string>();
        if (int.TryParse(settings.LogRotator.Delete_Days, out var days))
        {
            double dec = -1 * days;
            now = now.AddDays(dec);
            delete.AddRange(from file in GetFileNames() let lol = GetDateTime(file) where GetDateTime(file) < now && file.EndsWith(".zip") select Path.Combine(GetDirectory(), file));
        }

        foreach (var file in delete)
        {
            var path = Path.Combine(GetDirectory(), file);
            if (!File.Exists(path)) continue;

            File.Delete(path);
        }
    }
}
