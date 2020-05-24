using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.MediaInfo;
using Shoko.Server.Models;
using Stream = Shoko.Models.PlexAndKodi.Stream;
// ReSharper disable StringLiteralTypo

// ReSharper disable InconsistentNaming

namespace Shoko.Server.FileHelper.Subtitles
{
    public class SubtitleHelper
    {
        public static List<TextStream> GetSubtitleStreams(SVR_VideoLocal_Place vplace)
        {
            List<TextStream> ls = new VobSubSubtitles().GetStreams(vplace);
            ls.AddRange(new TextSubtitles().GetStreams(vplace));
            return ls;
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