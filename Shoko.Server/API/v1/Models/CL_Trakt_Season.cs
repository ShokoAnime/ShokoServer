using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_Trakt_Season
{
    public int Trakt_SeasonID { get; set; }
    public int Trakt_ShowID { get; set; }
    public int Season { get; set; }
    public string URL { get; set; }
    public List<CL_Trakt_Episode> Episodes { get; set; }
}
