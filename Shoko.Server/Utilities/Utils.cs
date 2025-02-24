using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Filters;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Quartz.Logging;
using Shoko.Models.Enums;
using Shoko.Server.API.SignalR.NLog;
using Shoko.Server.Extensions;
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

    public static void ShowErrorMessage(Exception ex, string message = null)
    {
        _logger.Error(ex, message);
    }

    public static void ShowErrorMessage(string msg)
    {
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

    public static int GetScheduledHours(ScheduledUpdateFrequency freq)
    {
        return freq switch
        {
            ScheduledUpdateFrequency.HoursSix => 6,
            ScheduledUpdateFrequency.HoursTwelve => 12,
            ScheduledUpdateFrequency.Daily => 24,
            ScheduledUpdateFrequency.WeekOne => 24 * 7,
            ScheduledUpdateFrequency.MonthOne => 24 * 30,
            _ => int.MaxValue,
        };
    }

    public static bool IsVideo(string fileName)
    {
        var videoExtensions = SettingsProvider.GetSettings().Import.VideoExtensions
            .Select(ext => ext.Trim().ToUpper())
            .WhereNotDefault()
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

        return videoExtensions.Contains(Path.GetExtension(fileName).Replace(".", string.Empty).Trim());
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
