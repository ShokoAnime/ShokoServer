using System;

namespace Shoko.Server.Providers.AniDB
{
    public class BannedEventArgs : EventArgs
    {
        public bool Banned { get; set; }
        public int TimeSecs { get; set; }
        public string Reason { get; set; }
    }
}