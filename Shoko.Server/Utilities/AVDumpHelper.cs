using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NLog;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Readers;
using Shoko.Commons.Utils;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server
{
    public static class AVDumpHelper
    {
        public static readonly string destination = Path.Combine(ServerSettings.ApplicationPath, "Utilities");
        public static readonly string avdumpRarDestination = Path.Combine(destination, "avdump2.rar");

        public const string AVDump2URL = @"http://static.anidb.net/client/avdump2/avdump2_6714.rar";
        public static readonly string avdumpDestination = Path.Combine(destination, "AVDump2CL.exe");

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static bool GetAndExtractAVDump()
        {
            if (File.Exists(avdumpRarDestination)) return ExtractAVDump();
            if (!DownloadFile(AVDump2URL, avdumpRarDestination)) return false;
            return ExtractAVDump();
        }

        private static bool ExtractAVDump()
        {
            try
            {
                using (var archive = RarArchive.Open(avdumpRarDestination))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                        entry.WriteToDirectory(destination, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                }
                File.Delete(avdumpRarDestination);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static bool DownloadFile(string sourceURL, string fileName)
        {
            try
            {
                if (File.Exists(fileName)) return true;
                using (Stream stream = Misc.DownloadWebBinary(sourceURL))
                {
                    if (stream == null) return false;
                    string destinationFolder = Directory.GetParent(fileName).FullName;
                    if (!Directory.Exists(destinationFolder)) Directory.CreateDirectory(destinationFolder);

                    using (var fileStream = File.Create(fileName))
                    {
                        CopyStream(stream, fileStream);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, len);
        }

        public static string DumpFile(int vid)
        {
            var vl = RepoFactory.VideoLocal.GetByID(vid);
            if (vl == null) return "Unable to get videoloocal with id: " + vid;
            string file = vl.GetBestVideoLocalPlace(true)?.FullServerPath;
            if (string.IsNullOrEmpty(file)) return "Unable to get file: " + vid;
            return DumpFile(file);
        }

        public static string DumpFile(string file)
        {
            try
            {
                if (!File.Exists(avdumpDestination) && !GetAndExtractAVDump())
                    return "Could not find  or download AvDump2 CLI";
                if (string.IsNullOrEmpty(file))
                    return "File path cannot be null";
                if (!File.Exists(file))
                    return "Could not find Video File: " + file;

                var filenameArgs = GetFilenameAndArgsForOS(file);

                logger.Info($"Dumping File with AVDump: {filenameArgs.Item1} {filenameArgs.Item2}");
                
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
                string strOutput = pProcess.StandardOutput.ReadToEnd();

                //Wait for process to finish
                pProcess.WaitForExit();

                return strOutput;
            }
            catch (Exception ex)
            {
                logger.Error($"An error occurred while AVDumping the file \"file\":\n{ex}");
                return $"An error occurred while AVDumping the file:\n{ex}";
            }
        }

        private static Tuple<string, string> GetFilenameAndArgsForOS(string file)
        {
            // Windows: avdumpDestination --Auth=....
            // Mono: mono avdumpDestination --Auth=...
            var executable = avdumpDestination;
            string fileName = (char)34 + file + (char)34;

            var args = $"--Auth={ServerSettings.Instance.AniDb.Username.Trim()}:" +
                       $"{ServerSettings.Instance.AniDb.AVDumpKey.Trim()}" +
                       $" --LPort={ServerSettings.Instance.AniDb.AVDumpClientPort} --PrintEd2kLink -t {fileName}";

            if (Utils.IsRunningOnMono())
            {
                executable = "mono";
                args = $"{avdumpDestination} {args}";
            }

            return Tuple.Create(executable, args);
        }
    }
}