using System;
using System.Collections.Generic;
using System.IO;

namespace Shoko.Server.MediaInfo.Subtitles;

public class VobSubSubtitles : ISubtitles
{
    public List<TextStream> GetStreams(FileInfo file)
    {
        var streams = new List<TextStream>();
        var language = SubtitleHelper.GetLanguageFromFilename(file.Name);

        var m = MediaInfoUtility.GetMediaInfo(file.FullName);
        var tStreams = m?.TextStreams;
        if (tStreams == null || tStreams.Count == 0) tStreams = new List<TextStream> { new() };

        tStreams.ForEach(a =>
        {
            a.External = true;
            a.Filename = file.Name;
            if (language == null)
            {
                return;
            }

            a.Language = language;
            var mapping = MediaInfoUtility.GetLanguageMapping(language);
            if (mapping == null)
            {
                return;
            }

            a.LanguageCode = mapping.Item1;
            a.LanguageName = mapping.Item2;
        });
        streams.AddRange(tStreams);
        return streams;
    }

    public bool IsSubtitleFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower().TrimStart('.');
        return ext.Equals("idx", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals("sub", StringComparison.OrdinalIgnoreCase);
    }
}
