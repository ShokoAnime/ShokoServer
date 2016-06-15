using System.Collections.Generic;
using JMMContracts.KodiContracts;

namespace JMMFileHelper.Subtitles
{
    public interface IKodiSubtitles
    {
        List<Stream> Process(string filename);
        List<Stream> GetStreams(string filename);
    }
}