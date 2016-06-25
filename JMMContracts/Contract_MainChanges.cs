using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts
{
    public class Contract_MainChanges
    {
        public Contract_Changes<Contract_GroupFilter> Filters { get; set; }=new Contract_Changes<Contract_GroupFilter>();
        public Contract_Changes<Contract_AnimeGroup> Groups { get; set; }=new Contract_Changes<Contract_AnimeGroup>();
        public Contract_Changes<Contract_AnimeSeries> Series { get; set; }=new Contract_Changes<Contract_AnimeSeries>();
        public DateTime LastChange { get; set; }
    }
}
