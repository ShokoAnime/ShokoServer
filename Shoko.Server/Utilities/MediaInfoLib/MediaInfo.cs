using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Models.MediaInfo;
using Shoko.Server.Extensions;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities.MediaInfoLib;

public static class MediaInfo
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static MediaContainer GetMediaInfo_New(string filename)
    {
        try
        {
            var exe = GetMediaInfoPathForOS();

            var pProcess = GetProcess(exe, filename);
            pProcess.Start();
            var output = pProcess.StandardOutput.ReadToEnd().Trim();
            //Wait for process to finish
            pProcess.WaitForExit();

            if (pProcess.ExitCode != 0 || !output.StartsWith("{"))
            {
                // We have an error
                if (string.IsNullOrWhiteSpace(output) || output.EqualsInvariantIgnoreCase("null"))
                {
                    output = pProcess.StandardError.ReadToEnd().Trim();
                }

                if (string.IsNullOrWhiteSpace(output) || output.EqualsInvariantIgnoreCase("null"))
                {
                    output = "No message";
                }

                Logger.Error($"MediaInfo threw an error on {filename}, {exe}: {output}");
                return null;
            }

            var settings = new JsonSerializerSettings
            {
                Converters = new JsonConverter[]
                {
                    new StreamJsonConverter(), new BooleanConverter(), new StringEnumConverter(),
                    new DateTimeConverter { DateTimeFormat = "yyyy-MM-dd HH:mm:ss" }, new MultiIntConverter(),
                    new MenuBase64Converter()
                },
                Error = (_, e) =>
                {
                    Logger.Error(e.ErrorContext.Error);
                    e.ErrorContext.Handled = true;
                }
            };

            // assuming json, as it starts with {
            var m = JsonConvert.DeserializeObject<MediaContainer>(output, settings);
            if (m == null)
            {
                throw new Exception($"Unable to deserialize MediaInfo response: {output}");
            }

            m.media.track.ForEach(a =>
            {
                // Stream should never be null, but here we are
                if (string.IsNullOrEmpty(a?.Language))
                {
                    return;
                }

                var languages = MediaInfoUtils.GetLanguageMapping(a.Language);
                if (languages == null)
                {
                    Logger.Error($"{filename} had a missing language code: {a.Language}");
                    return;
                }

                a.LanguageCode = languages.Item1;
                a.LanguageName = languages.Item2;
            });
            return m;
        }
        catch (Exception e)
        {
            Logger.Error($"MediaInfo threw an error on {filename}: {e}");
            return null;
        }
    }

    private static Process GetProcess(string processName, string filename)
    {
        var pProcess = new Process
        {
            StartInfo =
            {
                FileName = processName,
                ArgumentList = { "--OUTPUT=JSON", filename },
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        return pProcess;
    }

    private static string GetMediaInfoPathForOS()
    {
        var envVar = Environment.GetEnvironmentVariable("MEDIAINFO_PATH");
        var path = string.Empty;
        if (!string.IsNullOrEmpty(envVar))
        {
            // Allow spesifying an executable name other than "mediainfo"
            if (!envVar.Contains(Path.DirectorySeparatorChar) && !envVar.Contains(Path.AltDirectorySeparatorChar))
                return envVar;
            // Resolve the path from the application's data directory if the
            // path is not an absolute path.
            path = Path.Combine(Utils.ApplicationPath, envVar);
            if (File.Exists(path)) return path;
        }

        var settings = Utils.SettingsProvider.GetSettings();
        path = settings.Import.MediaInfoPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;

        if (Utils.IsRunningOnLinuxOrMac()) return "mediainfo";

        var exePath = Assembly.GetEntryAssembly()?.Location;
        var exeDir = Path.GetDirectoryName(exePath);
        if (exeDir == null) return null;

        var appPath = Path.Combine(exeDir, "MediaInfo", "MediaInfo.exe");
        if (!File.Exists(appPath)) return null;

        if (path == null)
        {
            settings.Import.MediaInfoPath = appPath;
            Utils.SettingsProvider.SaveSettings();
        }

        return appPath;
    }

    public static MediaContainer GetMediaInfo(string filename)
    {
        MediaContainer m = null;
        var mediaTask = Task.FromResult(GetMediaInfo_New(filename));

        var timeout = Utils.SettingsProvider.GetSettings().Import.MediaInfoTimeoutMinutes;
        if (timeout > 0)
        {
            var task = Task.WhenAny(mediaTask, Task.Delay(TimeSpan.FromMinutes(timeout))).Result;
            if (task == mediaTask)
            {
                m = mediaTask.Result;
            }
        }
        else
        {
            m = mediaTask.Result;
        }

        return m;
    }
}
