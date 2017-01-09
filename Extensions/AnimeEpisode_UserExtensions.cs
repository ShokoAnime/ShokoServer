using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class AnimeEpisode_UserExtensions
    {
        public static bool IsWatched(this AnimeEpisode_User epuser)
        {
            return epuser.WatchedCount > 0;
        }
    }
}
