using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shoko.Commons.Extensions;
using Shoko.Models.MediaInfo;
using Shoko.Server.Models;
// ReSharper disable StringLiteralTypo

// ReSharper disable InconsistentNaming

namespace Shoko.Server.FileHelper.Subtitles
{
    public static class SubtitleHelper
    {
        private static List<ISubtitles> SubtitleImplementations;

        public static List<TextStream> GetSubtitleStreams(SVR_VideoLocal_Place vplace)
        {
            if (SubtitleImplementations == null) SubtitleImplementations = InitImplementations();

            var path = vplace.FullServerPath;
            if (string.IsNullOrEmpty(path)) return new List<TextStream>();
            string directoryName = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directoryName)) return new List<TextStream>();
            if (!Directory.Exists(directoryName)) return new List<TextStream>();
            DirectoryInfo directory = new DirectoryInfo(directoryName);

            var basename = Path.GetFileNameWithoutExtension(path);

            var streams = new List<TextStream>();
            foreach (FileInfo file in directory.EnumerateFiles())
            {
                // Make sure it's actually the subtitle for this video file
                if (!file.Name.StartsWith(basename)) continue;
                // Get streams for each implementation
                SubtitleImplementations.Where(implementation => implementation.IsSubtitleFile(file.Extension))
                    .SelectMany(implementation => implementation.GetStreams(file)).ForEach(streams.Add);
            }

            return streams;
        }

        private static List<ISubtitles> InitImplementations()
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                    .Where(x => typeof(ISubtitles).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                    .Select(type => (ISubtitles) Activator.CreateInstance(type)).ToList();
            }
            catch (Exception e)
            {
                return new List<ISubtitles>();
            }
        }

        public static string GetLanguageFromFilename(string path)
        {
            // sub format of filename.eng.srt
            int lastSeparator = path.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).LastIndexOfAny(new[]
                {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar});
            string filename = path.Substring(lastSeparator + 1);
            string[] parts = filename.Split('.');
            // if there aren't 3 parts, then it's not in the format for this to work
            if (parts.Length < 3) return null;
            // length - 1 is last, so - 2 is second to last
            string lang = parts[parts.Length - 2];

            switch (lang.Length)
            {
                case 2:
                    return lang;
                case 3:
                    return MediaInfoUtils.GetLanguageFromCode(lang) ?? lang;
                default:
                    return MediaInfoUtils.GetLanguageFromName(lang) ?? lang;
            }
        }

        public static string GetMediaInfoCompatibleFile(FileInfo file)
        {
            var f = File.OpenRead(file.FullName);
            byte[] utf16bePre = Encoding.BigEndianUnicode.GetPreamble();
            byte[] utf8Pre = Encoding.UTF8.GetPreamble();
            byte[] bytes = new byte[utf8Pre.Length];
            string tempfilepath = null;

            // Check first two bytes for utf16be preamble
            f.Read(bytes, 0, utf16bePre.Length);
            if (bytes[..utf16bePre.Length].SequenceEqual(utf16bePre))
            {
                // Convert to utf16le in temp file
                byte[] tempbytes = new byte[f.Length];
                bytes.CopyTo(tempbytes, 0);
                f.Read(tempbytes, utf16bePre.Length, tempbytes.Length - utf16bePre.Length);
                tempfilepath = Path.GetTempFileName();
                File.WriteAllBytes(tempfilepath, Encoding.Convert(Encoding.BigEndianUnicode, Encoding.Unicode, tempbytes));
            }
            else
            {
                // Check first three bytes for utf8 preamble
                f.Read(bytes, utf16bePre.Length, utf8Pre.Length - utf16bePre.Length);
                if (bytes.SequenceEqual(utf8Pre))
                {
                    // Remove utf8 preamble in temp file
                    byte[] restOfFile = new byte[f.Length - utf8Pre.Length];
                    f.Read(restOfFile);
                    tempfilepath = Path.GetTempFileName();
                    File.WriteAllBytes(tempfilepath, restOfFile);
                }
            }
            f.Close();
            return tempfilepath ?? file.FullName;
        }
    }
}