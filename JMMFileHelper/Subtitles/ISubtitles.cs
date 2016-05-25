﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMContracts.PlexAndKodi;

namespace JMMFileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<Stream> Process(string filename);
        List<Stream> GetStreams(string filename);

    }
}
