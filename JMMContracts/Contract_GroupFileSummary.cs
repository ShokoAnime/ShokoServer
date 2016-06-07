using System;
using System.Collections.Generic;

namespace JMMContracts
{
    public class Contract_GroupFileSummary : IComparable<Contract_GroupFileSummary>
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

        public int CompareTo(Contract_GroupFileSummary obj)
        {
            return GroupNameShort.CompareTo(obj.GroupNameShort);
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}/{2}", GroupNameShort, FileCountNormal, FileCountSpecials);
        }
    }
}