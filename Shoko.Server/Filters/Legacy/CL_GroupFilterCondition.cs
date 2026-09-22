#nullable disable
namespace Shoko.Server.Filters.Legacy;

public class CL_GroupFilterCondition
{
    public int GroupFilterConditionID { get; set; }
    public int GroupFilterID { get; set; }
    public int ConditionType { get; set; }
    public int ConditionOperator { get; set; }
    public string ConditionParameter { get; set; }
}
