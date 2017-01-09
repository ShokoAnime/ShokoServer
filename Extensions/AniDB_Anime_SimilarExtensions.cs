using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class AniDB_Anime_SimilarExtensions
    {
        public static double GetApprovalPercentage(this AniDB_Anime_Similar similar)
        {
            if (similar.Total == 0) return (double) 0;
            return (double) similar.Approval / (double) similar.Total * (double) 100;
        }
    }
}
