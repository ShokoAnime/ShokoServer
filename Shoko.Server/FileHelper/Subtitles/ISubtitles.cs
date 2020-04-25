using System.Collections.Generic;
using NutzCode.CloudFileSystem;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.Models;

namespace Shoko.Server.FileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<Stream> Process(SVR_VideoLocal_Place filename);
        List<Stream> GetStreams(IFile filename);
    }
}