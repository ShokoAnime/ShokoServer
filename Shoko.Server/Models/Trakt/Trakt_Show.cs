
namespace Shoko.Server.Models.Trakt;

public class Trakt_Show
{
    public int Trakt_ShowID { get; set; }
    public string TraktID { get; set; }
    public int? TmdbShowID { get; set; }
    public string Title { get; set; }
    public string Year { get; set; }
    public string URL { get; set; }
    public string Overview { get; set; }
}
