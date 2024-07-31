using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;
using NLog.Filters;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Quartz.Logging;
using Shoko.Models.Enums;
using Shoko.Server.API.SignalR.NLog;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities;

public static class Utils
{
    public static ShokoServer ShokoServer { get; set; }

    public static IServiceProvider ServiceContainer { get; set; }

    public static ISettingsProvider SettingsProvider { get; set; }

    private static string _applicationPath = null;

    public static string ApplicationPath
    {
        get
        {
            if (_applicationPath != null)
                return _applicationPath;

            var shokoHome = Environment.GetEnvironmentVariable("SHOKO_HOME");
            if (!string.IsNullOrWhiteSpace(shokoHome))
                return _applicationPath = Path.GetFullPath(shokoHome);

            if (IsLinux)
                return _applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".shoko",
                    DefaultInstance);

            return _applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                DefaultInstance);
        }
    }

    public static string DefaultInstance { get; set; } = Assembly.GetEntryAssembly().GetName().Name;

    public static string DefaultImagePath => Path.Combine(ApplicationPath, "images");

    public static string AnimeXmlDirectory { get; set; } = Path.Combine(ApplicationPath, "Anime_HTTP");

    public static string MyListDirectory { get; set; } = Path.Combine(ApplicationPath, "MyList");

    public static string GetDistinctPath(string fullPath)
    {
        var parent = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(parent) ? fullPath : Path.Combine(Path.GetFileName(parent), Path.GetFileName(fullPath));
    }

    private static string GetInstanceFromCommandLineArguments()
    {
        const int NotFound = -1;
        var args = Environment.GetCommandLineArgs();
        var idx = Array.FindIndex(args, x => string.Equals(x, "instance", StringComparison.InvariantCultureIgnoreCase));
        if (idx is NotFound)
            return null;
        if (idx >= args.Length - 1)
            return null;
        return args[idx + 1];
    }

    public static void SetInstance()
    {
        var instance = GetInstanceFromCommandLineArguments();
        if (string.IsNullOrWhiteSpace(instance) is false)
            DefaultInstance = instance;
    }

    public static void InitLogger()
    {
        var target = (FileTarget)LogManager.Configuration.FindTargetByName("file");
        if (target != null)
        {
            target.FileName = Utils.ApplicationPath + "/logs/${shortdate}.log";
        }

#if LOGWEB
            // Disable blackhole http info logs
            LogManager.Configuration.LoggingRules.FirstOrDefault(r => r.LoggerNamePattern.StartsWith("Microsoft.AspNetCore"))?.DisableLoggingForLevel(LogLevel.Info);
            LogManager.Configuration.LoggingRules.FirstOrDefault(r => r.LoggerNamePattern.StartsWith("Shoko.Server.API.Authentication"))?.DisableLoggingForLevel(LogLevel.Info);
#endif
#if DEBUG
        // Enable debug logging
        LogManager.Configuration.LoggingRules.FirstOrDefault(a => a.Targets.Contains(target))
            ?.EnableLoggingForLevel(LogLevel.Debug);
#endif

        var signalrTarget =
            new AsyncTargetWrapper(
                new SignalRTarget { Name = "signalr", MaxLogsCount = 5000, Layout = "${message}${onexception:\\: ${exception:format=tostring}}" }, 50,
                AsyncTargetWrapperOverflowAction.Discard);
        LogManager.Configuration.AddTarget("signalr", signalrTarget);
        LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, signalrTarget));
        var consoleTarget = LogManager.Configuration.FindTargetByName<ColoredConsoleTarget>("console");
        if (consoleTarget != null)
        {
            consoleTarget.Layout = "${date:format=HH\\:mm\\:ss}| ${logger:shortname=true} --- ${message}${onexception:\\: ${exception:format=tostring}}";
        }

        foreach (var loggingRule in LogManager.Configuration.LoggingRules)
        {
            if (loggingRule.Targets.Contains(target) || loggingRule.Targets.Contains(consoleTarget) || loggingRule.Targets.Contains(signalrTarget))
            {
                loggingRule.FilterDefaultAction = FilterResult.Log;
                loggingRule.Filters.Add(new ConditionBasedFilter()
                {
                    Action = FilterResult.Ignore,
                    Condition = "(contains(message, 'password') or contains(message, 'token') or contains(message, 'key')) and starts-with(message, 'Settings.')"
                });
            }
        }

        LogProvider.SetLogProvider(new NLog.Extensions.Logging.NLogLoggerFactory());
        
        LogManager.ReconfigExistingLoggers();
    }

    public static void SetTraceLogging(bool enabled)
    {
        var fileRule = LogManager.Configuration.LoggingRules.FirstOrDefault(a => a.Targets.Any(b => b is FileTarget));
        var signalrRule = LogManager.Configuration.LoggingRules.FirstOrDefault(a => a.Targets.Any(b => b is SignalRTarget));
        if (enabled)
        {
            fileRule?.EnableLoggingForLevels(LogLevel.Trace, LogLevel.Debug);
            signalrRule?.EnableLoggingForLevels(LogLevel.Trace, LogLevel.Debug);
        }
        else
        {
            fileRule?.DisableLoggingForLevels(LogLevel.Trace, LogLevel.Debug);
            signalrRule?.DisableLoggingForLevels(LogLevel.Trace, LogLevel.Debug);
        }

        LogManager.ReconfigExistingLoggers();
    }

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public class ErrorEventArgs : EventArgs
    {
        public string Message { get; internal set; }

        public string Title { get; internal set; }

        public bool IsError { get; internal set; } = true;
    }

    public class CancelReasonEventArgs : CancelEventArgs
    {
        public CancelReasonEventArgs(string reason, string formTitle)
        {
            FormTitle = formTitle;
            Reason = reason;
        }

        public string Reason { get; }
        public string FormTitle { get; }
    }

    public static event EventHandler<ErrorEventArgs> ErrorMessage;

    public static event EventHandler OnEvents;

    public delegate void DispatchHandler(Action a);

    public static event DispatchHandler OnDispatch;

    public static void DoEvents()
    {
        OnEvents?.Invoke(null, null);
    }

    public static void MainThreadDispatch(Action a)
    {
        if (OnDispatch != null)
        {
            OnDispatch?.Invoke(a);
        }
        else
        {
            a();
        }
    }

    public static void ShowErrorMessage(Exception ex, string message = null)
    {
        ErrorMessage?.Invoke(null, new ErrorEventArgs { Message = message ?? ex.Message });
        _logger.Error(ex, message);
    }

    public static void ShowErrorMessage(string msg)
    {
        ErrorMessage?.Invoke(null, new ErrorEventArgs { Message = msg });
        _logger.Error(msg);
    }

    public static void ShowErrorMessage(string title, string msg)
    {
        ErrorMessage?.Invoke(null, new ErrorEventArgs { Message = msg, Title = title });
        _logger.Error(msg);
    }

    public static string GetApplicationVersion(Assembly a = null)
    {
        a ??= Assembly.GetExecutingAssembly();
        return a.GetName().Version.ToString();
    }

    public static Dictionary<string, string> GetApplicationExtraVersion(Assembly assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();
        if (assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) is not AssemblyInformationalVersionAttribute version)
            return [];

        return version.InformationalVersion.Split(",")
                .Select(raw => raw.Split("="))
                .Where(pair => pair.Length == 2 && !string.IsNullOrEmpty(pair[1]))
                .ToDictionary(pair => pair[0], pair => pair[1]);
    }

    public static string ReplaceInvalidFolderNameCharacters(string folderName)
    {
        var ret = folderName.Replace(@"*", "\u2605"); // ★ (BLACK STAR)
        ret = ret.Replace(@"|", "\u00a6"); // ¦ (BROKEN BAR)
        ret = ret.Replace(@"\", "\u29F9"); // ⧹ (BIG REVERSE SOLIDUS)
        ret = ret.Replace(@"/", "\u2044"); // ⁄ (FRACTION SLASH)
        ret = ret.Replace(@":", "\u0589"); // ։ (ARMENIAN FULL STOP)
        ret = ret.Replace("\"", "\u2033"); // ″ (DOUBLE PRIME)
        ret = ret.Replace(@">", "\u203a"); // › (SINGLE RIGHT-POINTING ANGLE QUOTATION MARK)
        ret = ret.Replace(@"<", "\u2039"); // ‹ (SINGLE LEFT-POINTING ANGLE QUOTATION MARK)
        ret = ret.Replace(@"?", "\uff1f"); // ？ (FULL WIDTH QUESTION MARK)
        ret = ret.Replace(@"...", "\u2026"); // … (HORIZONTAL ELLIPSIS)
        if (ret.StartsWith('.'))
        {
            ret = string.Concat("․", ret.AsSpan(1, ret.Length - 1));
        }

        if (ret.EndsWith('.')) // U+002E
        {
            ret = string.Concat(ret.AsSpan(0, ret.Length - 1), "․"); // U+2024
        }

        return ret.Trim();
    }

    public static string GetOSInfo()
    {
        return RuntimeInformation.OSDescription;
    }

    // Returns the human-readable file size for an arbitrary, 64-bit file size
    // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
    // http://www.somacon.com/p576.php
    public static string FormatByteSize(long fileSize)
    {
        // Get absolute value
        var absolute_i = fileSize < 0 ? -fileSize : fileSize;
        // Determine the suffix and readable value
        string suffix;
        double readable;
        if (absolute_i >= 0x1000000000000000) // Exabyte
        {
            suffix = "EB";
            readable = fileSize >> 50;
        }
        else if (absolute_i >= 0x4000000000000) // Petabyte
        {
            suffix = "PB";
            readable = fileSize >> 40;
        }
        else if (absolute_i >= 0x10000000000) // Terabyte
        {
            suffix = "TB";
            readable = fileSize >> 30;
        }
        else if (absolute_i >= 0x40000000) // Gigabyte
        {
            suffix = "GB";
            readable = fileSize >> 20;
        }
        else if (absolute_i >= 0x100000) // Megabyte
        {
            suffix = "MB";
            readable = fileSize >> 10;
        }
        else if (absolute_i >= 0x400) // Kilobyte
        {
            suffix = "KB";
            readable = fileSize;
        }
        else
        {
            return fileSize.ToString("0 B"); // Byte
        }

        // Divide by 1024 to get fractional value
        readable = readable / 1024;
        // Return formatted number with suffix
        return readable.ToString("0.### ") + suffix;
    }

    public static int GetVideoWidth(string videoResolution)
    {
        var videoWidth = 0;
        if (videoResolution.Trim().Length > 0)
        {
            var dimensions = videoResolution.Split('x');
            if (dimensions.Length > 0)
            {
                int.TryParse(dimensions[0], out videoWidth);
            }
        }

        return videoWidth;
    }

    public static int GetVideoHeight(string videoResolution)
    {
        var videoHeight = 0;
        if (videoResolution.Trim().Length > 0)
        {
            var dimensions = videoResolution.Split('x');
            if (dimensions.Length > 1)
            {
                int.TryParse(dimensions[1], out videoHeight);
            }
        }

        return videoHeight;
    }

    public static int GetScheduledHours(ScheduledUpdateFrequency freq)
    {
        switch (freq)
        {
            case ScheduledUpdateFrequency.Daily:
                return 24;
            case ScheduledUpdateFrequency.HoursSix:
                return 6;
            case ScheduledUpdateFrequency.HoursTwelve:
                return 12;
            case ScheduledUpdateFrequency.WeekOne:
                return 24 * 7;
            case ScheduledUpdateFrequency.MonthOne:
                return 24 * 30;
            case ScheduledUpdateFrequency.Never:
                return int.MaxValue;
        }

        return int.MaxValue;
    }

    public static void GetFilesForImportFolder(DirectoryInfo sDir, ref List<string> fileList)
    {
        try
        {
            if (sDir == null)
            {
                _logger.Error("Filesystem not found");
                return;
            }
            // get root level files

            if (!sDir.Exists)
            {
                _logger.Error($"Unable to retrieve folder {sDir.FullName}");
                return;
            }

            fileList.AddRange(sDir.GetFiles().Select(a => a.FullName));

            // search sub folders
            foreach (var dir in sDir.GetDirectories())
            {
                GetFilesForImportFolder(dir, ref fileList);
            }
        }
        catch (Exception excpt)
        {
            _logger.Error(excpt.Message);
        }
    }

    public static bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
    {
        try
        {
            using (File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
            {
                return true;
            }
        }
        catch
        {
            if (throwIfFails)
            {
                throw;
            }

            return false;
        }
    }

    public static bool IsLinux
    {
        get
        {
            var p = (int)Environment.OSVersion.Platform;
            return p == 4 || p == 6 || p == 128;
        }
    }

    public static bool IsRunningOnLinuxOrMac()
    {
        return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    /// <summary>
    /// Determines an encoded string's encoding by analyzing its byte order mark (BOM).
    /// Defaults to ASCII when detection of the text file's endianness fails.
    /// </summary>
    /// <param name="data">Byte array of the encoded string</param>
    /// <returns>The detected encoding.</returns>
    public static Encoding GetEncoding(byte[] data)
    {
        if (data.Length < 4)
        {
            return Encoding.ASCII;
        }
        // Analyze the BOM
#pragma warning disable SYSLIB0001
        if (data[0] == 0x2b && data[1] == 0x2f && data[2] == 0x76)
        {
            return Encoding.UTF7;
        }

        if (data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf)
        {
            return Encoding.UTF8;
        }

        if (data[0] == 0xff && data[1] == 0xfe)
        {
            return Encoding.Unicode; //UTF-16LE
        }

        if (data[0] == 0xfe && data[1] == 0xff)
        {
            return Encoding.BigEndianUnicode; //UTF-16BE
        }

        if (data[0] == 0 && data[1] == 0 && data[2] == 0xfe && data[3] == 0xff)
        {
            return Encoding.UTF32;
        }

        return Encoding.ASCII;
#pragma warning restore SYSLIB0001
    }
}
