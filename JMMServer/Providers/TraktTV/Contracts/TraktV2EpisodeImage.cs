using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2EpisodeImage
    {
        [DataMember(Name = "screenshot")]
        public TraktV2EpisodeScreenshot screenshot { get; set; }
    }
}
