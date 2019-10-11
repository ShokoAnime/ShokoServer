using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Configuration;
using Newtonsoft.Json;
using NLog;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.Extensions;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public static class MediaInfo
    {
        private static string WrapperPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "MediaInfoWrapper.exe");

        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static Media GetMediaInfoFromWrapper(string filename)
        {
            try
            {
                var filenameArgs = GetFilenameAndArgsForOS(filename);

                logger.Trace($"Calling MediaInfoWrapper for file: {filenameArgs.Item1} {filenameArgs.Item2}");
                
                Process pProcess = new Process
                {
                    StartInfo =
                    {
                        FileName = filenameArgs.Item1,
                        Arguments = filenameArgs.Item2,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                pProcess.Start();
                string strOutput = pProcess.StandardOutput.ReadToEnd().Trim();
                //Wait for process to finish
                pProcess.WaitForExit();
                if (pProcess.ExitCode != 0)
                {
                    string error = pProcess.StandardError.ReadToEnd().Trim();
                    if (!strOutput.EqualsInvariantIgnoreCase("null")) error = strOutput + " " + error;
                    if (error.EqualsInvariantIgnoreCase("null")) error = "No message";
                    logger.Error($"MediaInfo crashed on {filename}: {error}");
                    return null;
                }
                
                if (!strOutput.StartsWith("{"))
                {
                    // We have an error
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