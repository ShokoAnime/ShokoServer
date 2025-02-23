namespace Shoko.Models.Client
{
    public class CL_AnimeGroup_Save_Request
    {
        public int? AnimeGroupID { get; set; } 
        public int? AnimeGroupParentID { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public int IsFave { get; set; }
        public int IsManuallyNamed { get; set; }
        public string SortName { get; set; }
        public int OverrideDescription { get; set; }
    }
}