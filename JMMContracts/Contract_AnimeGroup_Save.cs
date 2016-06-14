namespace JMMContracts
{
    public class Contract_AnimeGroup_Save
    {
        public int? AnimeGroupID { get; set; } // will be NULL for a new group
        public int? AnimeGroupParentID { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public int IsFave { get; set; }
        public int IsManuallyNamed { get; set; }
        public string SortName { get; set; }
        public int OverrideDescription { get; set; }
    }
}