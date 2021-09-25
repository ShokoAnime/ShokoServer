using System.Collections.Generic;
using Shoko.Server.Providers;

namespace Shoko.Server.Repositories
{
    public interface IEpisodeGenericRepo
    {
        List<GenericEpisode> GetByProviderID(string providerId);
        int GetNumberOfEpisodesForSeason(string providerId, int season);
        int GetLastSeasonForSeries(string providerId);
        GenericEpisode GetByEpisodeProviderID(string episodeproviderId);
        GenericEpisode GetByProviderIdSeasonAnEpNumber(string providerId, int season, int epNumber);
    }
}
