﻿using System.Collections.Generic;

namespace JMMContracts
{
    public class Contract_AnimeSeriesFileStats
    {
        public int AnimeSeriesID { get; set; }
        public string AnimeSeriesName { get; set; }        
        public List<string> Folders { get; set; }
        public int FileCount { get; set; }
        public long FileSize { get; set; }
    }
}