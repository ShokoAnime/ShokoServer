using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class GroupFilterConditionExtensions
    {
        public static GroupFilterConditionType GetConditionTypeEnum(this GroupFilterCondition grpf)
        {
            return (GroupFilterConditionType) grpf.ConditionType;
        }

        public static GroupFilterOperator GetConditionOperatorEnum(this GroupFilterCondition grpf)
        {
            return (GroupFilterOperator) grpf.ConditionOperator;
        }
    }
}
