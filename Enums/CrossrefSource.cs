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
        Good = 2,
        Bad = 3,
        Ugly = 4,
        SarahJessicaParker = 5, // http://southpark.wikia.com/wiki/Sarah_Jessica_Parker
    }
}
