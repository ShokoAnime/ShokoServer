using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class AnimeGroupExtensions
    {
        public static bool GetHasMissingEpisodesAny(this AnimeGroup grp)
        {
            return grp.MissingEpisodeCount > 0 || grp.MissingEpisodeCountGroups > 0;
        }

        public static bool GetHasMissingEpisodesGroups(this AnimeGroup gr)
        {
            return gr.MissingEpisodeCountGroups > 0;
        }

        public static bool GetHasMissingEpisodes(this AnimeGroup grp)
        {
            return grp.MissingEpisodeCountGroups > 0;
        }
    }
}
