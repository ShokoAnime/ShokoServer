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
        public static readonly string Destination = Path.Combine(ServerSettings.ApplicationPath, "Utilities", "AVDump");
        public static readonly string AVDumpZipDestination = Path.Combine(Destination, "avdump2.zip");

        public const string AVDump2URL = @"http://static.anidb.net/client/avdump2/avdump2_7100.zip";
        
        public static readonly string avdumpDestination = Path.Combine(Destination, "AVDump2CL.exe");

        public static readonly string[] OldAVDump =
        {
            "AVDump2CL.exe", "AVDump2CL.exe.config", "AVDump2Lib.dll", "AVDump2Lib.dll.config", "CSEBMLLib.dll",
            "Ionic.Zip.Reduced.dll", "libMediaInfo_x64.so", "libMediaInfo_x86.so", "MediaInfo_x64.dll",
            "MediaInfo_x86.dll", "Error"
        };

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static bool GetAndExtractAVDump()
        {
            if (File.Exists(AVDumpZipDestination)) return ExtractAVDump();
            if (!DownloadFile(AVDump2URL, AVDumpZipDestination)) return false;
            return ExtractAVDump();
        }

        private static bool ExtractAVDump()
        {
            try
            {
                // First clear out the existing one. 
                DeleteOldAVDump();

                // Now make the new one
                using (Stream stream = File.OpenRead(AVDumpZipDestination))
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            reader.WriteEntryToDirectory(Destination, new ExtractionOptions()
                            {
                                // This may have serious problems in the future, but for now, AVDump is flat
                                ExtractFullPath = false,
                                Overwrite = true
                            });
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            try
            {
                File.Delete(AVDumpZipDestination);
            }
            catch
            {
                // eh we tried
            }
            return true;
        }
        
        private static void DeleteOldAVDump()
        {
            var oldPath = Directory.GetParent(Destination).FullName;
            foreach (string name in OldAVDump)
            {
                try
                {
                    var path = Path.Combine(oldPath, name);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        continue;
                    }
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                    // Eh we tried
                }
            }
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