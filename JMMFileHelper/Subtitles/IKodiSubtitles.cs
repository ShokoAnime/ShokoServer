using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts.KodiContracts;

namespace JMMFileHelper.Subtitles
{
    public interface IKodiSubtitles
    {
        List<Stream> Process(string filename);
        List<Stream> GetStreams(string filename);

    }
}
