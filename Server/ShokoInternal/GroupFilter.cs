namespace Shoko.Models.Server
{
    public class GroupFilter
    {
        public int GroupFilterID { get; set; }
        public string GroupFilterName { get; set; }
        public int ApplyToSeries { get; set; }
        public int BaseCondition { get; set; }
        public string SortingCriteria { get; set; }
        public int? Locked { get; set; }
        public int FilterType { get; set; }
        public int? ParentGroupFilterID { get; set; }
        public int InvisibleInClients { get; set; }
    }
}
