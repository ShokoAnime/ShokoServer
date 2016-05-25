﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2SearchTvDBIDShowResult
    {
        [DataMember(Name = "type")]
        public string type { get; set; }

        [DataMember(Name = "score")]
        public string score { get; set; }

        [DataMember(Name = "show")]
        public TraktV2Show show { get; set; }

        [DataMember(Name = "episode")]
        public TraktV2Episode episode { get; set; }

        public SearchIDType ResultType
        {
            get
            {
                if (type.Equals("show", StringComparison.InvariantCultureIgnoreCase)) return SearchIDType.Show;

                return SearchIDType.Episode;
            }
        }
    }
}
