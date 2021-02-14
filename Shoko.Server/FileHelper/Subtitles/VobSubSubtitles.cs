using System;
using System.Collections.Generic;
using System.IO;
using NutzCode.CloudFileSystem;
using Shoko.Models.MediaInfo;
using Shoko.Server.Models;
using Shoko.Server.Utilities.MediaInfoLib;

namespace Shoko.Server.FileHelper.Subtitles
{
    public class VobSubSubtitles : ISubtitles
    {
        public List<TextStream> GetStreams(FileInfo file)
        {
            List<TextStream> streams = new List<TextStream>();
            var language = SubtitleHelper.GetLanguageFromFilename(file.Name);

            MediaContainer m = MediaInfo.GetMediaInfo(file.FullName);
            List<TextStream> tStreams = m?.TextStreams;
            if (tStreams == null || tStreams.Count <= 0) return streams;
            tStreams.ForEach(a =>
            {
                a.External = true;
                a.Filename = file.Name;
                if (language == null) return;
                a.Language = language;
                Tuple<string,string> mapping = MediaInfoUtils.GetLanguageMapping(language);
                if (mapping == null) return;
                a.LanguageCode = mapping.Item1;
                a.LanguageName = mapping.Item2;
            });
            streams.AddRange(tStreams);
            return streams;
        }
        
        public bool IsSubtitleFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower().TrimStart('.');
            return ext.Equals("idx", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals("sub", StringComparison.OrdinalIgnoreCase);
        }
    }
}