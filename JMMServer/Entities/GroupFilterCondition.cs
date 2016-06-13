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

        public override string ToString()
        {
            return string.Format("{0} - {1} - {2} - {3}", GroupFilterConditionID, ConditionType, ConditionOperator,
                ConditionParameter);
        }

        public GroupFilterConditionType ConditionTypeEnum
        {
            get { return (GroupFilterConditionType) ConditionType; }
        }

        public GroupFilterOperator ConditionOperatorEnum
        {
            get { return (GroupFilterOperator) ConditionOperator; }
        }

        public Contract_GroupFilterCondition ToContract()
        {
            Contract_GroupFilterCondition contract = new Contract_GroupFilterCondition();
            contract.GroupFilterConditionID = this.GroupFilterConditionID;
            contract.GroupFilterID = this.GroupFilterID;
            contract.ConditionType = this.ConditionType;
            contract.ConditionOperator = this.ConditionOperator;
            contract.ConditionParameter = this.ConditionParameter;
            return contract;
        }
    }
}