using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
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
            episode = new TraktV2Episode
            {
                ids = new TraktV2EpisodeIds
                {
                    trakt = traktId.ToString(),
                    slug = slugId
                },
                season = season,
                number = episodeNumber
            };
        }
    }
}