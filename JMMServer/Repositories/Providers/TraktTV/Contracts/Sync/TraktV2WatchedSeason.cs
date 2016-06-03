using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2WatchedSeason
    {
        [DataMember(Name = "number")]
        public int number { get; set; }

        [DataMember(Name = "episodes")]
        public List<TraktV2WatchedEpisode> episodes { get; set; }

        public override string ToString()
        {
            if (episodes != null)
                return string.Format("Season: {0} - Episodes Watched: {1}", number, episodes.Count);
            else
                return string.Format("Season: {0}", number);
        }
    }
}
