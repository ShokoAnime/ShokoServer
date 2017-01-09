using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class AnimeGroup_UserExtensions
    {
        public static bool GetHasUnwatchedFiles(this AnimeGroup_User grpuser) => grpuser.UnwatchedEpisodeCount > 0;
        public static bool GetAllFilesWatched(this AnimeGroup_User grpuser) => grpuser.UnwatchedEpisodeCount == 0;
        public static bool GetAnyFilesWatched(this AnimeGroup_User grpuser) => grpuser.WatchedEpisodeCount > 0;
    }
}
