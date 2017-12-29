namespace AniDBAPI
{
    public class Raw_AniDB_AnimeDesc
    {
        public string Description { get; set; }

        // constructor
        // sRecMessage is the message received from ANIDB file info command
        public Raw_AniDB_AnimeDesc(string sRecMessage)
        {
            // remove the header info
            string[] sDetails = sRecMessage.Substring(14).Split('|');

            // 233 ANIMEDESC

            //  {int4 current part}|{int4 max parts}|{str description} 
            //  0. 0 ** current part
            //  1. 1 ** max parts
            //  2. Blah blah ** description

            this.Description = AniDBAPILib.ProcessAniDBString(sDetails[2]);
        }

        public Raw_AniDB_AnimeDesc()
        {
            Description = string.Empty;
        }

        public override string ToString()
        {
            return string.Format("Raw_AniDB_AnimeDesc:: Description: {0}", Description);
        }
    }
}