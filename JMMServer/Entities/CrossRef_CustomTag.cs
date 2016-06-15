using JMMContracts;

namespace JMMServer.Entities
{
    public class CrossRef_CustomTag
    {
        public int CrossRef_CustomTagID { get; private set; }
        public int CustomTagID { get; set; }
        public int CrossRefID { get; set; }
        public int CrossRefType { get; set; }

        public Contract_CrossRef_CustomTag ToContract()
        {
            Contract_CrossRef_CustomTag ctag = new Contract_CrossRef_CustomTag();

            ctag.CrossRef_CustomTagID = CrossRef_CustomTagID;
            ctag.CustomTagID = CustomTagID;
            ctag.CrossRefID = CrossRefID;
            ctag.CrossRefType = CrossRefType;

            return ctag;
        }
    }
}