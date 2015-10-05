namespace JMMModels.Childs
{
    public class GroupFilterCondition
    {
        public GroupFilterConditionType Type { get; set; }
        public GroupFilterOperator Operator { get; set; }
        public string Parameter { get; set; }
    }
}
