﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2CollectedEpisode
    {
        [DataMember(Name = "number")]
        public int number { get; set; }

        [DataMember(Name = "collected_at")]
        public string collected_at { get; set; }

        public override string ToString()
        {
            return string.Format("Ep#: {0} - Collected At: {1}", number, collected_at);
        }
    }
}
