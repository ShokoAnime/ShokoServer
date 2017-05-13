using System;
using System.IO;

namespace Shoko.Commons.Downloads
{
    public class Torrent
    {

        private const int StatusStarted = 1;
        private const int StatusChecking = 2;
        private const int StatusStartAfterCheck = 4;
        private const int StatusChecked = 8;
        private const int StatusError = 16;
        private const int StatusPaused = 32;
        private const int StatusQueued = 64;
        private const int StatusLoaded = 128;

        public string Hash { get; set; }
        public int Status { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// integer in per mils
        /// </summary>
        public long PercentProgress { get; set; }

        public string PercentProgressFormatted
        {
            get
            {
                double pro = (double)PercentProgress / (double)10;
                return $"{pro:0.0}%";
            }
        }

        /// <summary>
        /// integer in bytes
        /// </summary>
        public long Downloaded { get; set; }

        /// <summary>
        /// integer in bytes
        /// </summary>
        public long Uploaded { get; set; }


        /// <summary>
        /// integer in per mils
        /// </summary>
        public long Ratio { get; set; }

        /// <summary>
        /// integer in bytes per second
        /// </summary>
        public long UploadSpeed { get; set; }

        /// <summary>
        /// integer in bytes per second
        /// </summary>
        public long DownloadSpeed { get; set; }

        /// <summary>
        /// integer in seconds
        /// </summary>
        public long ETA { get; set; }
        public string Label { get; set; }
        public long PeersConnected { get; set; }
        public long PeersInSwarm { get; set; }
        public long SeedsConnected { get; set; }
        public long SeedsInSwarm { get; set; }

        /// <summary>
        /// integer in 1/65535ths
        /// </summary>
        public long Availability { get; set; }

        public long TorrentQueueOrder { get; set; }

        /// <summary>
        /// integer in bytes
        /// </summary>
        public long Remaining { get; set; }

        public Torrent()
        {
        }

        public Torrent(object[] row)
        {
            Hash = row[0].ToString();
            Status = int.Parse(row[1].ToString());
            Name = row[2].ToString();
            Size = long.Parse(row[3].ToString());
            PercentProgress = long.Parse(row[4].ToString());
            Downloaded = long.Parse(row[5].ToString());
            Uploaded = long.Parse(row[6].ToString());
            Ratio = long.Parse(row[7].ToString());
            UploadSpeed = long.Parse(row[8].ToString());
            DownloadSpeed = long.Parse(row[9].ToString());
            ETA = long.Parse(row[10].ToString());
            Label = row[11].ToString();
            PeersConnected = long.Parse(row[12].ToString());
            PeersInSwarm = long.Parse(row[13].ToString());
            SeedsConnected = long.Parse(row[14].ToString());
            SeedsInSwarm = long.Parse(row[15].ToString());
            Availability = long.Parse(row[16].ToString());
            TorrentQueueOrder = long.Parse(row[17].ToString());
            Remaining = long.Parse(row[18].ToString());
        }

        public override string ToString()
        {
            return $"Torrent: {Name} - {PercentProgressFormatted} - {Status} - {Hash}";
        }

        public string SeedsFormatted => $"{SeedsConnected} ({SeedsInSwarm})";

        public string PeersFormatted => $"{PeersConnected} ({PeersInSwarm})";

        public string SizeFormatted => Shoko.Commons.Utils.Misc.FormatByteSize(Size);

        public string DownloadSpeedFormatted => Shoko.Commons.Utils.Misc.FormatByteSize((long)DownloadSpeed) + "/sec";

        public string UploadSpeedFormatted => Shoko.Commons.Utils.Misc.FormatByteSize((long)UploadSpeed) + "/sec";

        public string DownloadedFormatted => Shoko.Commons.Utils.Misc.FormatByteSize((long)Downloaded);

        public string UploadedFormatted => Shoko.Commons.Utils.Misc.FormatByteSize((long)Uploaded);

        public string RatioFormatted
        {
            get
            {
                double temp = (double)Ratio / (double)1000;
                return $"{temp:0.000}";
            }
        }

        public bool IsRunning
        {
            get
            {
                if (Status == 137 || Status == 200 || Status == 201) return true;
                return false;
            }
        }

        public bool IsNotRunning
        {
            get
            {
                if (Status == 136) return true;
                return false;
            }
        }

        public bool IsPaused
        {
            get
            {
                int paused = Status & StatusPaused;
                if (paused > 0) return true;
                return false;
            }
        }

        public string StatusFormatted
        {
            get
            {
                if (Status == 201 && Remaining > 0) return "Downloading";
                if (Status == 201 && Remaining == 0) return "Seeding";
                if (Status == 137 && Remaining > 0) return "[F] Downloading";
                if (Status == 137 && Remaining == 0) return "[F] Seeding";

                if (Status == 200 && Remaining > 0) return "Queued";
                if (Status == 200 && Remaining == 0) return "Queued Seed";

                if (Status == 136 && Remaining == 0) return "Finished";
                if (Status == 136 && Remaining > 0) return "Stopped";

                int paused = Status & StatusPaused;
                if (paused > 0)
                    return "Paused";

                return "";
            }
        }

        public string StatusImage
        {
            get
            {
                if (Status == 201 && Remaining > 0) return @"/Images/Torrents/tor_downloading.png";
                if (Status == 201 && Remaining == 0) return @"/Images/Torrents/tor_seeding.png";
                if (Status == 137 && Remaining > 0) return @"/Images/Torrents/tor_downloading.png";
                if (Status == 137 && Remaining == 0) return @"/Images/Torrents/tor_seeding.png";

                if (Status == 200 && Remaining > 0) return @"/Images/Torrents/tor_queued.png";
                if (Status == 200 && Remaining == 0) return @"/Images/Torrents/tor_queuedseed.png";

                if (Status == 136 && Remaining == 0) return @"/Images/Torrents/tor_finished.png";
                if (Status == 136 && Remaining > 0) return @"/Images/Torrents/tor_stopped.png";

                int paused = Status & StatusPaused;
                if (paused > 0) return @"/Images/Torrents/tor_paused.png";

                return @"/Images/32_key.png";
            }
        }

        public string ListDisplay
        {
            get
            {
                if (StatusFormatted.Length > 0)
                    return $"{StatusFormatted.ToUpper()}: {Name} - {PercentProgressFormatted}";
                else
                    return $"{Name} - {PercentProgressFormatted}";
            }
        }

        public string ClosestAnimeMatchString
        {
            get
            {

                try
                {
                    string match = Path.GetFileNameWithoutExtension(Name);

                    //remove any group names or CRC's
                    while (true)
                    {
                        int pos = match.IndexOf('[');
                        if (pos >= 0)
                        {
                            int endPos = match.IndexOf(']', pos);
                            if (endPos >= 0)
                            {
                                string rubbish = match.Substring(pos, endPos - pos + 1);
                                match = match.Replace(rubbish, "");
                            }
                            else break;
                        }
                        else break;
                    }

                    //remove any video information
                    while (true)
                    {
                        int pos = match.IndexOf('(');
                        if (pos >= 0)
                        {
                            int endPos = match.IndexOf(')', pos);
                            if (endPos >= 0)
                            {
                                string rubbish = match.Substring(pos, endPos - pos + 1);
                                match = match.Replace(rubbish, "");
                            }
                            else break;
                        }
                        else break;
                    }

                    match = match.Replace("_", " ");

                    int pos2 = match.IndexOf('-');
                    if (pos2 >= 1)
                    {
                        match = match.Substring(0, pos2).Trim();
                    }

                    return match;
                }
                catch
                {
                    return "";
                }
            }
        }

    }
}
