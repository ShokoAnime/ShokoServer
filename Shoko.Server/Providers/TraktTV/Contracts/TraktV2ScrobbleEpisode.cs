using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
internal class TraktV2ScrobbleEpisode
{
    [DataMember(Name = "episode")]
    public TraktV2Episode Episode { get; set; }

    [DataMember(Name = "progress")]
    public float Progress { get; set; }

    public void Init(float progressVal, int? traktId, string slugId, int season, int episodeNumber)
    {
        Progress = progressVal;
        Episode = new TraktV2Episode
        {
            IDs = new TraktV2EpisodeIds { trakt = traktId.ToString(), slug = slugId },
            SeasonNumber = season,
            EpisodeNumber = episodeNumber
        };
    }
}
