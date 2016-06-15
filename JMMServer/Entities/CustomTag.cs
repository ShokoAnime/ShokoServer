using JMMContracts;

namespace JMMServer.Entities
{
    public class CustomTag
    {
        public int CustomTagID { get; private set; }
        public string TagName { get; set; }
        public string TagDescription { get; set; }

        public Contract_CustomTag ToContract()
        {
            Contract_CustomTag ctag = new Contract_CustomTag();

            ctag.CustomTagID = CustomTagID;
            ctag.TagName = TagName;
            ctag.TagDescription = TagDescription;

            return ctag;
        }
    }
}