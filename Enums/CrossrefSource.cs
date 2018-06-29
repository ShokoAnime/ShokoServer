using System;

namespace Shoko.Models.Enums
{
    public enum CrossRefSource
    {
        Automatic = 0,
        AniDB = 1,
        User = 2,
        WebCache = 3,
    }

    public enum MatchRating
    {
        UserVerified = 1,
        Good = 2, // Dates and Titles match, give or take
        Mkay = 3, // Dates Matched
        Bad = 4, // Titles Matched
        Ugly = 5, // Neither Matched, but we could fill from adjacent episodes
        SarahJessicaParker = 6, // http://southpark.wikia.com/wiki/Sarah_Jessica_Parker
    }
}
