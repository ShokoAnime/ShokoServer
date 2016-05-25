﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2ShowWatchedResult
    {
        [DataMember(Name = "plays")]
        public int plays { get; set; }

        [DataMember(Name = "last_watched_at")]
        public string last_watched_at { get; set; }

        [DataMember(Name = "show")]
        public TraktV2Show show { get; set; }

        [DataMember(Name = "seasons")]
        public TraktV2WatchedSeason[] seasons { get; set; }

        public override string ToString()
        {
            if (show != null)
                return string.Format("{0} - Last Watched: {1}", show.Title, last_watched_at);
            else
                return string.Format("TraktV2ShowWatchedResult - Last Watched: {0}", last_watched_at);
        }
    }
}
