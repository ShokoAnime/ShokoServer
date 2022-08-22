using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Models.MediaInfo;
using Shoko.Server.Extensions;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public static class MediaInfo
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
                        output = pProcess.StandardError.ReadToEnd().Trim();

                    if (string.IsNullOrWhiteSpace(output) || output.EqualsInvariantIgnoreCase("null"))
                        output = "No message";

                    logger.Error($"MediaInfo threw an error on {filename}, {exe}: {output}");
                    return null;
                }

                var settings = new JsonSerializerSettings
                {
                    Converters = new JsonConverter[]
                    {
                        new StreamJsonConverter(), new BooleanConverter(), new StringEnumConverter(),
                        new DateTimeConverter() {DateTimeFormat = "yyyy-MM-dd HH:mm:ss"}, new MultiIntConverter()
                    },
                    Error = (s, e) =>
                    {
                        logger.Error(e.ErrorContext.Error);
                        e.ErrorContext.Handled = true;
                    }
                };

                // assuming json, as it starts with {
                var m = JsonConvert.DeserializeObject<MediaContainer>(output, settings);
                m.media.track.ForEach(a =>
                {
                    // Stream should never be null, but here we are
                    if (string.IsNullOrEmpty(a?.Language)) return;
                    var langs = MediaInfoUtils.GetLanguageMapping(a.Language);
                    if (langs == null)
                    {
                        logger.Error($"{filename} had a missing language code: {a.Language}");
                        return;
                    }
                    a.LanguageCode = langs.Item1;
                    a.LanguageName = langs.Item2;
                });
                return m;
            }
            catch (Exception e)
            {
                logger.Error($"MediaInfo threw an error on {filename}: {e}");
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
            if (ServerSettings.Instance.Import.MediaInfoPath != null &&
                File.Exists(ServerSettings.Instance.Import.MediaInfoPath))
                return ServerSettings.Instance.Import.MediaInfoPath;

            if (Utils.IsRunningOnLinuxOrMac()) return "mediainfo";

            var exePath = Assembly.GetEntryAssembly()?.Location;
            var exeDir = Path.GetDirectoryName(exePath);
            if (exeDir == null) return null;
            var appPath = Path.Combine(exeDir, "MediaInfo", "MediaInfo.exe");
            if (!File.Exists(appPath)) return null;
            if (ServerSettings.Instance.Import.MediaInfoPath == null)
            {
                ServerSettings.Instance.Import.MediaInfoPath = appPath;
                ServerSettings.Instance.SaveSettings();
            }
            return appPath;

        }

        public static MediaContainer GetMediaInfo(string filename)
        {
            MediaContainer m = null;
            var mediaTask = Task.FromResult(GetMediaInfo_New(filename));

            var timeout = ServerSettings.Instance.Import.MediaInfoTimeoutMinutes;
            if (timeout > 0)
            {
                var task = Task.WhenAny(mediaTask, Task.Delay(TimeSpan.FromMinutes(timeout))).Result;
                if (task == mediaTask) m = mediaTask.Result;
            }
            else
            {
                m = mediaTask.Result;
            }
            
            return m;
        }
    }
}
