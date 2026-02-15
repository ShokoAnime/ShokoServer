using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_SeasonComparator : IComparer<string>
{
    private readonly Dictionary<string, int> _seasons = new()
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
        var partsX = x.Split(' ');
        var partsY = y.Split(' ');
        if (partsX.Length != 2 && partsY.Length != 2) return 0;
        if (partsX.Length != 2) return 1;
        if (partsY.Length != 2) return -1;
        var intX = int.TryParse(partsX[1], out var yearX);
        var intY = int.TryParse(partsY[1], out var yearY);
        if (!intX && !intY) return 0;
        if (!intX) return 1;
        if (!intY) return -1;
        var result = yearX.CompareTo(yearY);
        if (result != 0) return result;
        if (!_seasons.ContainsKey(partsX[0]) && !_seasons.ContainsKey(partsY[0])) return 0;
        if (!_seasons.ContainsKey(partsX[0])) return 1;
        if (!_seasons.ContainsKey(partsY[0])) return -1;
        return _seasons[partsX[0]].CompareTo(_seasons[partsY[0]]);
    }
}
