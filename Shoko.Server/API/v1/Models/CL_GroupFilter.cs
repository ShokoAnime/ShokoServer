using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_GroupFilter
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
    public List<CL_GroupFilterCondition> FilterConditions { get; set; }
    public Dictionary<int, HashSet<int>> Groups { get; set; }
    public Dictionary<int, HashSet<int>> Series { get; set; }
    public HashSet<int> Childs { get; set; }
}
