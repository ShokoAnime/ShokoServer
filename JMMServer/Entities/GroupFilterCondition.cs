using JMMContracts;

namespace JMMServer.Entities
{
    public class GroupFilterCondition
    {
        public int GroupFilterConditionID { get; private set; }
        public int GroupFilterID { get; set; }
        public int ConditionType { get; set; }
        public int ConditionOperator { get; set; }
        public string ConditionParameter { get; set; }

        public GroupFilterConditionType ConditionTypeEnum
        {
            get { return (GroupFilterConditionType)ConditionType; }
        }

        public GroupFilterOperator ConditionOperatorEnum
        {
            get { return (GroupFilterOperator)ConditionOperator; }
        }

        public override string ToString()
        {
            return string.Format("{0} - {1} - {2} - {3}", GroupFilterConditionID, ConditionType, ConditionOperator,
                ConditionParameter);
        }

        public Contract_GroupFilterCondition ToContract()
        {
            var contract = new Contract_GroupFilterCondition();
            contract.GroupFilterConditionID = GroupFilterConditionID;
            contract.GroupFilterID = GroupFilterID;
            contract.ConditionType = ConditionType;
            contract.ConditionOperator = ConditionOperator;
            contract.ConditionParameter = ConditionParameter;
            return contract;
        }
    }
}