﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2ShowCollectedResult
    {
        [DataMember(Name = "last_collected_at")]
        public string last_collected_at { get; set; }

        [DataMember(Name = "show")]
        public TraktV2Show show { get; set; }

        [DataMember(Name = "seasons")]
        public List<TraktV2CollectedSeason> seasons { get; set; }

        public override string ToString()
        {
            if (show != null)
                return string.Format("{0} - Last Collected: {1}", show.Title, last_collected_at);
            else
                return string.Format("TraktV2ShowCollectedResult - Last Watched: {0}", last_collected_at);
        }
    }
}
