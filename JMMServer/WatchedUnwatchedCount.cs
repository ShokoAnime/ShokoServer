namespace JMMServer
{
    public class WatchedUnwatchedCount
    {
        public WatchedUnwatchedCount()
        {
            WatchedCount = 0;
            UnwatchedCount = 0;
            WatchedCountEpisodes = 0;
            UnwatchedCountEpisodes = 0;
            WatchedCountCredits = 0;
            UnwatchedCountCredits = 0;
            WatchedCountSpecials = 0;
            UnwatchedCountSpecials = 0;
            WatchedCountTrailer = 0;
            UnwatchedCountTrailer = 0;
            WatchedCountParody = 0;
            UnwatchedCountParody = 0;
            WatchedCountOther = 0;
            UnwatchedCountOther = 0;
        }

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
    }
}