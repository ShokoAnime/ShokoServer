namespace AniDBAPI
{
    public class AniDBAPILib
    {
        public static int ProcessAniDBInt(string fld)
        {
            var iVal = 0;
            int.TryParse(fld, out iVal);
            return iVal;
        }

        public static long ProcessAniDBLong(string fld)
        {
            long iVal = 0;
            long.TryParse(fld, out iVal);
            return iVal;
        }

        public static string ProcessAniDBString(string fld)
        {
            var ret = fld.Trim();

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