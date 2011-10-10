using System;
using System.Collections.Generic;
using System.Text;

namespace AniDBAPI
{
	public class AniDBAPILib
	{
		public static int ProcessAniDBInt(string fld)
		{
			int iVal = 0;
			int.TryParse(fld, out iVal);
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
