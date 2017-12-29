using System;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Seiyuu : XMLBase
    {
        public int SeiyuuID { get; set; }
        public string SeiyuuName { get; set; }
        public string PicName { get; set; }

        public Raw_AniDB_Seiyuu()
        {
            InitFields();
        }

        private void InitFields()
        {
            SeiyuuID = 0;
            SeiyuuName = string.Empty;
            PicName = string.Empty;
        }
    }
}