using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Commons.Extensions;
using Shoko.Models.MediaInfo;
using Shoko.Server.Models;
// ReSharper disable StringLiteralTypo

// ReSharper disable InconsistentNaming

namespace Shoko.Server.FileHelper.Subtitles
{
    public static class SubtitleHelper
    {
        public static List<TextStream> GetSubtitleStreams(SVR_VideoLocal_Place vplace)
        {
            List<TextStream> ls = new VobSubSubtitles().GetStreams(vplace);
            ls.AddRange(new TextSubtitles().GetStreams(vplace));
            ls.ForEach(a =>
            {
                a.External = true;
                string lang = GetLanguageFromFilename(vplace.FilePath);
                if (lang == null) return;
                a.Language = lang;
                Tuple<string,string> mapping = MediaInfoUtils.GetLanguageMapping(lang);
                if (mapping == null) return;
                a.LanguageCode = mapping.Item1;
                a.LanguageName = mapping.Item2;
            });
            return ls;
        }

        public static string GetLanguageFromFilename(string path)
        {
            // sub format of filename.eng.srt
            int lastSeparator = path.Trim(new [] {Path.PathSeparator, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar}).LastIndexOfAny(new[]
                {Path.PathSeparator, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar});
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

        public static readonly Dictionary<string, string> Extensions = new Dictionary<string, string>
        {
            {"utf", "text/plain"},
            {"utf8", "text/plain"},
            {"utf-8", "text/plain"},
            {"srt", "text/plain"},
            {"smi", "text/plain"},
            {"rt", "text/plain"},
            {"ssa", "text/plain"},
            {"aqt", "text/plain"},
            {"jss", "text/plain"},
            {"ass", "text/plain"},
            {"idx", "application/octet-stream"},
            {"sub", "application/octet-stream"},
            {"txt", "text/plain"},
            {"psb", "text/plain"}
        };
    }
}