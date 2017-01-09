namespace Shoko.Server
{
    public class WatchedUnwatchedCount
    {
        public int WatchedCount { get; set; }
        public int UnwatchedCount { get; set; }
        public int WatchedCountEpisodes { get; set; }
        public int UnwatchedCountEpisodes { get; set; }
        public int WatchedCountCredits { get; set; }
        public int UnwatchedCountCredits { get; set; }
        public int WatchedCountSpecials { get; set; }
        public int UnwatchedCountSpecials { get; set; }
        public int WatchedCountTrailer { get; set; }
        public int UnwatchedCountTrailer { get; set; }
        public int WatchedCountParody { get; set; }
        public int UnwatchedCountParody { get; set; }
        public int WatchedCountOther { get; set; }
        public int UnwatchedCountOther { get; set; }

        public WatchedUnwatchedCount()
        {
            this.WatchedCount = 0;
            this.UnwatchedCount = 0;
            this.WatchedCountEpisodes = 0;
            this.UnwatchedCountEpisodes = 0;
            this.WatchedCountCredits = 0;
            this.UnwatchedCountCredits = 0;
            this.WatchedCountSpecials = 0;
            this.UnwatchedCountSpecials = 0;
            this.WatchedCountTrailer = 0;
            this.UnwatchedCountTrailer = 0;
            this.WatchedCountParody = 0;
            this.UnwatchedCountParody = 0;
            this.WatchedCountOther = 0;
            this.UnwatchedCountOther = 0;
        }
    }
}