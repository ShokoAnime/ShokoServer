using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NLog;
using Shoko.Models.PlexAndKodi;
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
            string output = null;
            
            // TODO Find MediaInfo per platform, execute it with --OUTPUT=JSON, read the standard out, deserialize with the following settings
            
            var settings = new JsonSerializerSettings
            {
                Converters =  new JsonConverter[] {new StreamJsonConverter(), new BooleanConverter()},
                DateFormatString = "UTC yyyy-MM-dd HH:mm:ss"
            };

            // assuming json, as it starts with {
            MediaContainer m = JsonConvert.DeserializeObject<MediaContainer>(output, settings);
            return m;
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

        public static Media GetMediaInfo(string filename)
        {
            // if (Utils.IsRunningOnMono())
            //    return MediaInfoParserInternal.Convert(filename, ServerSettings.Instance.Import.MediaInfoTimeoutMinutes);
            return GetMediaInfoFromWrapper(filename);
        }
    }
}
