namespace Shoko.Server.Providers.AniDB
{
    public class AniDBAPILib
    {
        public static int ProcessAniDBInt(string fld)
        {
            int.TryParse(fld, out int iVal);
            return iVal;
        }

        public static long ProcessAniDBLong(string fld)
        {
            long.TryParse(fld, out long iVal);
            return iVal;
        }

        public static string ProcessAniDBString(string fld)
        {
            string ret = fld.Trim();

            // remove any html
            ret = ret.Replace(@"</br>", ".");
            ret = ret.Replace(@"< /br>", ".");
            ret = ret.Replace(@"</ br>", ".");
            ret = ret.Replace(@"<br />", ".");
            ret = ret.Replace(@"<br/>", ".");

            return ret;
        }
    }
}