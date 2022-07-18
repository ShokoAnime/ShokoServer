using System;

namespace Shoko.Server.Providers.AniDB
{
    [Serializable]
    public class AniDBBannedException : Exception
    {
        public UpdateType BanType { get; set; }
        public DateTime? BanExpires { get; set; }
    }
}
