using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_AnimeSearch
{
    public int AnimeID { get; set; }
    public string MainTitle { get; set; }
    public HashSet<string> Titles { get; set; }
    public bool SeriesExists { get; set; }
    public int? AnimeSeriesID { get; set; }
    public string AnimeSeriesName { get; set; }
    public int? AnimeGroupID { get; set; }
    public string AnimeGroupName { get; set; }

    public override string ToString()
    {
        return $"{AnimeID} - {MainTitle} - {Titles}";
    }
}
