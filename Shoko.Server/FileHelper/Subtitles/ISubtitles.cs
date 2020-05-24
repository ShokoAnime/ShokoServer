using System.Collections.Generic;
using NutzCode.CloudFileSystem;
using Shoko.Models.MediaInfo;
using Shoko.Server.Models;

namespace Shoko.Server.FileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<TextStream> GetStreams(SVR_VideoLocal_Place filename);
    }
}