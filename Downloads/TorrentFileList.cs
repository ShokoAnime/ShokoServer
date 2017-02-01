namespace Shoko.Commons.Downloads
{
    public class TorrentFileList
    {
        private int _build;
        public int build
        {
            get { return _build; }
            set { _build = value; }
        }

        private object[] _files;
        public object[] files
        {
            get { return _files; }
            set { _files = value; }
        }
    }

    public class TorrentFileListSub
    {
        private string _hash;
        public string hash
        {
            get { return _hash; }
            set { _hash = value; }
        }

        private object[] _files;
        public object[] files
        {
            get { return _files; }
            set { _files = value; }
        }
    }
}
