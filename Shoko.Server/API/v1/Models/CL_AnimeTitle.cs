using System;

namespace Shoko.Server.API.v1.Models;

public class CL_AnimeTitle : ICloneable
{
    public int AnimeID { get; set; }
    public string TitleType { get; set; }
    public string Language { get; set; }
    public string Title { get; set; }
    public object Clone()
    {
        return new CL_AnimeTitle
        {
            AnimeID = AnimeID,
            TitleType = TitleType,
            Language = Language,
            Title = Title
        };
    }
}
