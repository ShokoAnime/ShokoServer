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
using Directory = Pri.LongPath.Directory;
using File = Pri.LongPath.File;
using Path = Pri.LongPath.Path;

namespace Shoko.Server.Utilities
{
    public class AVDumpHelper
    {
        public static readonly string destination = Path.Combine(ServerSettings.ApplicationPath, "Utilities");
        public static readonly string avdumpRarDestination = Path.Combine(destination, "avdump2.rar");

        public static readonly string AVDump2URL = @"http://static.anidb.net/client/avdump2/avdump2_6714.rar";
        public static readonly string avdumpDestination = Path.Combine(destination, "AVDump2CL.exe");

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
                    {
                        entry.WriteToDirectory(destination, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
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
            {
                output.Write(buffer, 0, len);
            }
        }

        public static string DumpFile(int vid)
        {
            var vl = RepoFactory.VideoLocal.GetByID(vid);
            if (vl == null) return "Unable to get videoloocal with id: " + vid;
            string file = vl.GetBestVideoLocalPlace()?.FullServerPath;
            if (string.IsNullOrEmpty(file)) return "Unable to get file: " + vid;
            if (Utils.IsRunningOnMono()) return DumpFile_Mono(file);
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

                //Create process
                Process pProcess = new Process();
                pProcess.StartInfo.FileName = avdumpDestination;

                //strCommandParameters are parameters to pass to program
                string fileName = (char) 34 + file + (char) 34;

                pProcess.StartInfo.Arguments =
                    $@" --Auth={ServerSettings.AniDB_Username}:{ServerSettings.AniDB_AVDumpKey} --LPort={
                            ServerSettings.AniDB_AVDumpClientPort
                        } --PrintEd2kLink -t {fileName}";

                pProcess.StartInfo.UseShellExecute = false;
                pProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.StartInfo.CreateNoWindow = true;
                pProcess.Start();
                string strOutput = pProcess.StandardOutput.ReadToEnd();

                //Wait for process to finish
                pProcess.WaitForExit();

                return strOutput;
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Error($"An error occurred while AVDumping the file \"file\":\n{ex}");
                return $"An error occurred while AVDumping the file:\n{ex}";
            }
        }

        public static string DumpFile_Mono(string file)
        {
            return "Not supported on Mono yet...";
        }
    }
}