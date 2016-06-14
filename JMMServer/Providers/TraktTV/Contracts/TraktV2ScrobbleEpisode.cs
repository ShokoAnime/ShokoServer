using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    class TraktV2ScrobbleEpisode
    {
        [DataMember(Name = "episode")]
        public TraktV2Episode episode { get; set; }

        [DataMember(Name = "progress")]
        public float progress { get; set; }

        public void Init(float progressVal, int? traktId, string slugId, int season, int episodeNumber)
        {
            progress = progressVal;
            episode = new TraktV2Episode();
            episode.ids = new TraktV2EpisodeIds();
            episode.ids.trakt = (traktId).ToString();
            episode.ids.slug = slugId;
            episode.season = season;
            episode.number = episodeNumber;
        }
    }
}
