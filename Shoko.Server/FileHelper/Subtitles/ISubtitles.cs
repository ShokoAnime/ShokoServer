using System.Collections.Generic;
using System.IO;
using Shoko.Models.MediaInfo;

namespace Shoko.Server.FileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<TextStream> GetStreams(FileInfo file);

        bool IsSubtitleFile(string path);
    }
}