using Nancy.Rest.Annotations.Atributes;
using Shoko.Models;

namespace Shoko.Models.Server
{
    public class GroupFilterCondition
    {
        public GroupFilterCondition()
        {
        }
        public int GroupFilterConditionID { get; set; }
        public int GroupFilterID { get; set; }
        public int ConditionType { get; set; }
        public int ConditionOperator { get; set; }
        public string ConditionParameter { get; set; }

    }
}