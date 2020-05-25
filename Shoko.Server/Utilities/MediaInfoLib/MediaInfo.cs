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
        private static string WrapperPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "MediaInfoWrapper.dll");

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static MediaContainer GetMediaInfo_New(string filename)
        {
            try
            {
                string exe = GetMediaInfoPathForOS();
                string args = $"--OUTPUT=JSON \"{filename}\"";

                var pProcess = GetProcess(exe, args);
                pProcess.Start();
                string output = pProcess.StandardOutput.ReadToEnd().Trim();
                //Wait for process to finish
                pProcess.WaitForExit();

                if (pProcess.ExitCode != 0 || !output.StartsWith("{"))
                {
                    // We have an error
                    if (string.IsNullOrWhiteSpace(output) || output.EqualsInvariantIgnoreCase("null"))
                        output = pProcess.StandardError.ReadToEnd().Trim();

                    if (string.IsNullOrWhiteSpace(output) || output.EqualsInvariantIgnoreCase("null"))
                        output = "No message";

                    logger.Error($"MediaInfo threw an error on {filename}: {output}");
                    return null;
                }

                var settings = new JsonSerializerSettings
                {
                    Converters = new JsonConverter[]
                    {
                        new StreamJsonConverter(), new BooleanConverter(), new StringEnumConverter(),
                        new DateTimeConverter() {DateTimeFormat = "yyyy-MM-dd HH:mm:ss"}, new MultiIntConverter()
                    }
                };

                // assuming json, as it starts with {
                MediaContainer m = JsonConvert.DeserializeObject<MediaContainer>(output, settings);
                m.media.track.ForEach(a =>
                {
                    if (!string.IsNullOrEmpty(a.Language))
                    {
                        var langs = MediaInfoUtils.GetLanguageMapping(a.Language);
                        if (langs == null)
                        {
                            logger.Error($"{filename} had a missing language code: {a.Language}");
                            return;
                        }
                        a.LanguageCode = langs.Item1;
                        a.LanguageName = langs.Item2;
                    }
                });
                return m;
            }
            catch (Exception e)
            {
                logger.Error($"MediaInfo threw an error on {filename}: {e}");
                return null;
            }
        }

        public static Media GetMediaInfoFromWrapper(string filename)
        {
            try
            {
                var filenameArgs = GetFilenameAndArgsForOS(filename);

                logger.Trace($"Calling MediaInfoWrapper for file: {filenameArgs.Item1} {filenameArgs.Item2}");

                Process pProcess = GetProcess(filenameArgs.Item1, filenameArgs.Item2);

                pProcess.Start();
                string strOutput = pProcess.StandardOutput.ReadToEnd().Trim();
                //Wait for process to finish
                pProcess.WaitForExit();
                
                if (pProcess.ExitCode != 0 || !strOutput.StartsWith("{"))
                {
                    // We have an error
                    if (string.IsNullOrWhiteSpace(strOutput) || strOutput.EqualsInvariantIgnoreCase("null"))
                        strOutput = pProcess.StandardError.ReadToEnd().Trim();

                    if (string.IsNullOrWhiteSpace(strOutput) || strOutput.EqualsInvariantIgnoreCase("null"))
                        strOutput = "No message";
                    
                    logger.Error($"MediaInfo threw an error on {filename}: {strOutput}");
                    return null;
                }
                
                // assuming json, as it starts with {
                Media m = JsonConvert.DeserializeObject<Media>(strOutput,
                    new JsonSerializerSettings {Culture = CultureInfo.InvariantCulture});
                return m;
            }
            catch (Exception e)
            {
                logger.Error($"MediaInfo threw an error on {filename}: {e}");
                return null;
            }
        }

        private static Process GetProcess(string filename, string args)
        {
            Process pProcess = new Process
            {
                StartInfo =
                {
                    FileName = filename,
                    Arguments = args,
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
            if (Utils.IsRunningOnLinuxOrMac()) return "mediainfo";

            string appPath = Path.Combine(Assembly.GetExecutingAssembly().Location, "MediaInfo", "MediaInfo.exe");
            if (File.Exists(appPath)) return appPath;

            return null;
        }
        
        private static Tuple<string, string> GetFilenameAndArgsForOS(string file)
        {
            // Windows: avdumpDestination --Auth=....
            // Mono: mono avdumpDestination --Auth=...
            var executable = WrapperPath;
            string fileName = (char)34 + file + (char)34;

            int timeout = ServerSettings.Instance.Import.MediaInfoTimeoutMinutes;
            var args = $"{fileName} {timeout}";

            if (Utils.IsRunningOnMono())
            {
                executable = "mono";
                #if DEBUG
                args = $"--debug {WrapperPath} {args}";
                #else
                args = $"{WrapperPath} {args}";
                #endif
            }

            if (Utils.IsRunningOnLinuxOrMac())
            {
                executable = "dotnet";
                args = $"{WrapperPath} {args}";
            }

            return Tuple.Create(executable, args);
        }

        public static MediaContainer GetMediaInfo(string filename)
        {
            MediaContainer m = null;
            Task<MediaContainer> mediaTask = Task.FromResult(GetMediaInfo_New(filename));

            int timeout = ServerSettings.Instance.Import.MediaInfoTimeoutMinutes;
            if (timeout > 0)
            {
                Task task = Task.WhenAny(mediaTask, Task.Delay(TimeSpan.FromMinutes(timeout))).Result;
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
