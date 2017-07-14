using System.Collections.Generic;

namespace Shoko.Models
{
    public class SeasonComparator : IComparer<string>
    {
        private readonly Dictionary<string, int> Seasons = new Dictionary<string, int>()
        {
            {"Winter", 0},
            {"Spring", 1},
            {"Summer", 2},
            {"Fall", 3}
        };

        public int Compare(string x, string y)
        {
            if (string.IsNullOrEmpty(x) && string.IsNullOrEmpty(y)) return 0;
            if (string.IsNullOrEmpty(x)) return 1;
            if (string.IsNullOrEmpty(y)) return -1;
            string[] partsX = x.Split(' ');
            string[] partsY = y.Split(' ');
            if (partsX.Length != 2 && partsY.Length != 2) return 0;
            if (partsX.Length != 2) return 1;
            if (partsY.Length != 2) return -1;
            bool intX = int.TryParse(partsX[1], out int yearX);
            bool intY = int.TryParse(partsY[1], out int yearY);
            if (!intX && !intY) return 0;
            if (!intX) return 1;
            if (!intY) return -1;
            int result = yearX.CompareTo(yearY);
            if (result != 0) return result;
            if (!Seasons.ContainsKey(partsX[0]) && !Seasons.ContainsKey(partsY[0])) return 0;
            if (!Seasons.ContainsKey(partsX[0])) return 1;
            if(!Seasons.ContainsKey(partsY[0])) return -1;
            return Seasons[partsX[0]].CompareTo(Seasons[partsY[0]]);
        }
    }
}