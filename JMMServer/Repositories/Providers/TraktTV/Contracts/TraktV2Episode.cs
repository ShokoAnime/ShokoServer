﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2Episode
    {
        [DataMember(Name = "season")]
        public int season { get; set; }

        [DataMember(Name = "number")]
        public int number { get; set; }

        [DataMember(Name = "title")]
        public string title { get; set; }

        [DataMember(Name = "ids")]
        public TraktV2EpisodeIds ids { get; set; }

        [DataMember(Name = "images")]
        public TraktV2EpisodeImage images { get; set; }


    }
}
