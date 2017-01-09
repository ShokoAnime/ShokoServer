using System.Collections.Generic;
using JMMServer.Entities;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using NutzCode.CloudFileSystem;

namespace JMMServer.FileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<Stream> Process(VideoLocal_Place filename);
        List<Stream> GetStreams(IFile filename);
    }
}