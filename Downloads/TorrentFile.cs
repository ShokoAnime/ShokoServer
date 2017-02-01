using System.Data;


namespace Shoko.Commons.Downloads
{
    public class TorrentFile
    {
        private string fileName;
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        private long fileSize = 0;
        public long FileSize
        {
            get { return fileSize; }
            set { fileSize = value; }
        }

        private long downloaded = 0;
        public long Downloaded
        {
            get { return downloaded; }
            set { downloaded = value; }
        }

        private long priority = 0;
        public long Priority
        {
            get { return priority; }
            set { priority = value; }
        }

        public TorrentFile()
        {
        }

        public TorrentFile(DataRow row)
        {
            fileName = row[0].ToString();
            fileSize = long.Parse(row[1].ToString());
            downloaded = long.Parse(row[2].ToString());
            priority = long.Parse(row[3].ToString());
        }

        public override string ToString()
        {
            return $"Torrent File: {fileName} - {FileSizeFormatted}";
        }

        public string PriorityFormatted
        {
            get
            {
                switch (Priority)
                {
                    case 0: return "Skip";
                    case 1: return "Low";
                    case 2: return "Normal";
                    case 3: return "High";
                }

                return "";
            }
        }

        public string FileSizeFormatted => Shoko.Commons.Utils.Misc.FormatByteSize(fileSize);

        public string DownloadedFormatted => Shoko.Commons.Utils.Misc.FormatByteSize((long)downloaded);
    }
}
