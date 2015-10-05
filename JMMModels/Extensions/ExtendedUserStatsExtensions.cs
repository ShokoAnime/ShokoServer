using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels.Extensions
{
    public static class ExtendedUserStatsExtensions
    {
        public static bool HasUnwatchedEpisodes(this JMMModels.Childs.ExtendedUserStats us)
		{
			return us.UnwatchedEpisodeCount > 0;
		}

		public static bool AllEpisodesWatched(this JMMModels.Childs.ExtendedUserStats us)
        {
			return us.UnwatchedEpisodeCount == 0;
		}

		public static bool AnyEpisodeWatched(this JMMModels.Childs.ExtendedUserStats us)
        {
			return us.WatchedEpisodeCount > 0;
		}
    }
}
