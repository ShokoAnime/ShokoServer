using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var streams = new List<TextStream>();
            foreach (FileInfo file in directory.EnumerateFiles())
            {
                foreach (var implementation in SubtitleImplementations)
                {
                    if (!implementation.IsSubtitleFile(file.Extension)) continue;
                    var ls = implementation.GetStreams(file);
                    streams.AddRange(ls);
                }
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
    }
}