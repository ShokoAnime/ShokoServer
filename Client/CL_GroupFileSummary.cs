using System;
using System.Collections.Generic;

namespace Shoko.Models.Client
{
    public class CL_GroupFileSummary
    {
        public string GroupName { get; set; }
        public string GroupNameShort { get; set; }
        public int FileCountNormal { get; set; }
        public bool NormalComplete { get; set; }
        public int FileCountSpecials { get; set; }
        public bool SpecialsComplete { get; set; }

        public double TotalFileSize { get; set; }
        public long TotalRunningTime { get; set; }

        public List<int> NormalEpisodeNumbers { get; set; }
        public string NormalEpisodeNumberSummary { get; set; }


    }
}