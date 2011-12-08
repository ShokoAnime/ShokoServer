using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using NLog;
using System.Security.Cryptography;

namespace JMMServer
{
	public class Utils
	{
		public const int LastYear = 2050;

		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static string CalculateSHA1(string text, Encoding enc)
		{
			byte[] buffer = enc.GetBytes(text);
			SHA1CryptoServiceProvider cryptoTransformSHA1 =
			new SHA1CryptoServiceProvider();
			string hash = BitConverter.ToString(cryptoTransformSHA1.ComputeHash(buffer)).Replace("-", "");

			return hash;
		}

		/// <summary>
		/// Compute Levenshtein distance --- http://www.merriampark.com/ldcsharp.htm
		/// </summary>
		/// <param name="s"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static int LevenshteinDistance(string s, string t)
		{
			int n = s.Length; //length of s
			int m = t.Length; //length of t

			int[,] d = new int[n + 1, m + 1]; // matrix

			int cost; // cost

			// Step 1
			if (n == 0) return m;
			if (m == 0) return n;

			// Step 2
			for (int i = 0; i <= n; d[i, 0] = i++) ;
			for (int j = 0; j <= m; d[0, j] = j++) ;

			// Step 3
			for (int i = 1; i <= n; i++)
			{
				//Step 4
				for (int j = 1; j <= m; j++)
				{
					// Step 5
					cost = (t.Substring(j - 1, 1) == s.Substring(i - 1, 1) ? 0 : 1);

					// Step 6
					d[i, j] = System.Math.Min(System.Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
							  d[i - 1, j - 1] + cost);
				}
			}

			// Step 7
			return d[n, m];
		}

		// Function to display parent function
		public static string GetParentMethodName()
		{
			StackTrace stackTrace = new StackTrace();
			StackFrame stackFrame = stackTrace.GetFrame(2);
			MethodBase methodBase = stackFrame.GetMethod();
			return methodBase.Name;
		}

		public static void ShowErrorMessage(Exception ex)
		{
			MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			logger.ErrorException(ex.ToString(), ex);
		}

		public static void ShowErrorMessage(string msg)
		{
			MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			logger.Error(msg);
		}

		public static string GetApplicationVersion(Assembly a)
		{
			AssemblyName an = a.GetName();
			return an.Version.ToString();
		}


		public static string DownloadWebPage(string url)
		{
			try
			{
				//BaseConfig.MyAnimeLog.Write("DownloadWebPage called by: {0} - {1}", GetParentMethodName(), url);

				HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
				webReq.Timeout = 60000; // 60 seconds
				webReq.Proxy = null;
				webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
				webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

				HttpWebResponse WebResponse = (HttpWebResponse)webReq.GetResponse();

				Stream responseStream = WebResponse.GetResponseStream();
				String enco = WebResponse.CharacterSet;
				Encoding encoding = null;
				if (!String.IsNullOrEmpty(enco))
					encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
				if (encoding == null)
					encoding = Encoding.Default;
				StreamReader Reader = new StreamReader(responseStream, encoding);

				string output = Reader.ReadToEnd();

				//BaseConfig.MyAnimeLog.Write("DownloadWebPage: {0}", output);

				WebResponse.Close();
				responseStream.Close();

				return output;
			}
			catch (Exception ex)
			{
				string msg = "---------- ERROR IN DOWNLOAD WEB PAGE ---------" + Environment.NewLine +
					url + Environment.NewLine +
					ex.ToString() + Environment.NewLine + "------------------------------------";
				//BaseConfig.MyAnimeLog.Write(msg);

				// if the error is a 404 error it may mean that there is a bad series association
				// so lets log it to the web cache so we can investigate
				if (ex.ToString().Contains("(404) Not Found"))
				{
				}

				return "";
			}
		}

		public static Stream DownloadWebBinary(string url)
		{
			try
			{
				HttpWebResponse response = null;
				HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
				// Note: some network proxies require the useragent string to be set or they will deny the http request
				// this is true for instance for EVERY thailand internet connection (also needs to be set for banners/episodethumbs and any other http request we send)
				webReq.UserAgent = "Anime2MP";
				webReq.Timeout = 20000; // 20 seconds
				response = (HttpWebResponse)webReq.GetResponse();

				if (response != null) // Get the stream associated with the response.
					return response.GetResponseStream();
				else
					return null;
			}
			catch (Exception ex)
			{
				//BaseConfig.MyAnimeLog.Write(ex.ToString());
				return null;
			}
		}

		public static string GetAniDBDate(int secs)
		{
			if (secs == 0) return "";

			DateTime thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
			thisDate = thisDate.AddSeconds(secs);
			return thisDate.ToString("dd MMM yyyy", Globals.Culture);
		}

		public static DateTime? GetAniDBDateAsDate(int secs)
		{
			if (secs == 0) return null;

			DateTime thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
			thisDate = thisDate.AddSeconds(secs);
			return thisDate;
		}

		public static int GetAniDBDateAsSeconds(string dateXML)
		{
			// eg "2008-12-31" or "2008-12" or "2008"
			if (dateXML.Trim().Length < 4) return 0;

			string month = "1";
			string day = "1";

			string year = dateXML.Trim().Substring(0, 4);

			if (dateXML.Trim().Length > 4)
				month = dateXML.Trim().Substring(5, 2);

			if (dateXML.Trim().Length > 7)
				day = dateXML.Trim().Substring(8, 2);

			//BaseConfig.MyAnimeLog.Write("Date = {0}/{1}/{2}", year, month, day);


			DateTime actualDate = new DateTime(int.Parse(year), int.Parse(month), int.Parse(day), 0, 0, 0);
			//startDate = startDate.AddDays(-1);

			return GetAniDBDateAsSeconds(actualDate);
		}

		public static DateTime? GetAniDBDateAsDate(string dateInSeconds, int dateFlags)
		{
			// DateFlags
			// 0 = normal start and end date (2010-01-31)
			// 1 = start date is year-month (2010-01)
			// 2 = start date is a year (2010)
			// 4 = normal start date, year-month end date 
			// 8 = normal start date, year end date 
			// 10 = start date is a year (2010)
			// 16 = normal start and end date (2010-01-31)

			double secs = 0;
			double.TryParse(dateInSeconds, out secs);
			if (secs == 0) return null;

			DateTime thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
			thisDate = thisDate.AddSeconds(secs);

			// reconstruct using date flags
			int year = thisDate.Year;
			int month = thisDate.Month;
			int day = thisDate.Day;

			if (dateFlags == 2 || dateFlags == 10 || dateFlags == 1)
				month = 1;

			if (dateFlags == 1)
				day = 1;

			return new DateTime(year, month, day, 0, 0, 0); ;
		}

		public static int GetAniDBDateAsSeconds(DateTime? dtDate)
		{
			if (!dtDate.HasValue) return 0;

			DateTime startDate = new DateTime(1970, 1, 1, 0, 0, 0);
			TimeSpan ts = dtDate.Value - startDate;

			return (int)ts.TotalSeconds;
		}

		public static string AniDBDate(DateTime date)
		{
			TimeSpan sp = date.Subtract(new DateTime(1970, 1, 1, 0, 0, 0));
			return ((long)sp.TotalSeconds).ToString();
		}

		private static string[] escapes = { "SOURCE", "TAKEN", "FROM", "HTTP", "ANN", "ANIMENFO", "ANIDB", "ANIMESUKI" };

		public static string ReparseDescription(string description)
		{
			if (description == null || description.Length == 0) return "";

			string val = description;
			val = val.Replace("<br />", Environment.NewLine).Replace("<br/>", Environment.NewLine).Replace("<i>", "").
					Replace("</i>", "").Replace("<b>", "").Replace("</b>", "").Replace("[i]", "").Replace("[/i]", "").
					Replace("[b]", "").Replace("[/b]", "");
			val = val.Replace("<BR />", Environment.NewLine).Replace("<BR/>", Environment.NewLine).Replace("<I>", "").Replace("</I>", "").Replace("<B>", "").Replace("</B>", "").Replace("[I]", "").Replace("[/I]", "").
					Replace("[B]", "").Replace("[/B]", "");

			string vup = val.ToUpper();
			while ((vup.Contains("[URL")) || (vup.Contains("[/URL]")))
			{
				int a = vup.IndexOf("[URL");
				if (a >= 0)
				{
					int b = vup.IndexOf("]", a + 1);
					if (b >= 0)
					{
						val = val.Substring(0, a) + val.Substring(b + 1);
						vup = val.ToUpper();
					}
				}
				a = vup.IndexOf("[/URL]");
				if (a >= 0)
				{
					val = val.Substring(0, a) + val.Substring(a + 6);
					vup = val.ToUpper();
				}
			}
			while (vup.Contains("HTTP:"))
			{
				int a = vup.IndexOf("HTTP:");
				if (a >= 0)
				{
					int b = vup.IndexOf(" ", a + 1);
					if (b >= 0)
					{
						if (vup[b + 1] == '[')
						{
							int c = vup.IndexOf("]", b + 1);
							val = val.Substring(0, a) + " " + val.Substring(b + 2, c - b - 2) + val.Substring(c + 1);
						}
						else
						{
							val = val.Substring(0, a) + val.Substring(b);
						}
						vup = val.ToUpper();
					}
					else
					{
						break;
					}
				}
			}
			int d = -1;
			do
			{
				if (d + 1 >= vup.Length)
					break;
				d = vup.IndexOf("[", d + 1);
				if (d != -1)
				{
					int b = vup.IndexOf("]", d + 1);
					if (b != -1)
					{
						string cont = vup.Substring(d, b - d);
						bool dome = false;
						foreach (string s in escapes)
						{
							if (cont.Contains(s))
							{
								dome = true;
								break;
							}
						}
						if (dome)
						{
							val = val.Substring(0, d) + val.Substring(b + 1);
							vup = val.ToUpper();
						}
					}
				}
			} while (d != -1);
			d = -1;
			do
			{
				if (d + 1 >= vup.Length)
					break;

				d = vup.IndexOf("(", d + 1);
				if (d != -1)
				{
					int b = vup.IndexOf(")", d + 1);
					if (b != -1)
					{
						string cont = vup.Substring(d, b - d);
						bool dome = false;
						foreach (string s in escapes)
						{
							if (cont.Contains(s))
							{
								dome = true;
								break;
							}
						}
						if (dome)
						{
							val = val.Substring(0, d) + val.Substring(b + 1);
							vup = val.ToUpper();
						}
					}
				}
			} while (d != -1);
			d = vup.IndexOf("SOURCE:");
			if (d == -1)
				d = vup.IndexOf("SOURCE :");
			if (d > 0)
			{
				val = val.Substring(0, d);
			}
			return val.Trim();
		}

		public static string FormatSecondsToDisplayTime(int secs)
		{
			TimeSpan t = TimeSpan.FromSeconds(secs);

			if (t.Hours > 0)
				return string.Format("{0}:{1}:{2}", t.Hours, t.Minutes.ToString().PadLeft(2, '0'), t.Seconds.ToString().PadLeft(2, '0'));
			else
				return string.Format("{0}:{1}", t.Minutes, t.Seconds.ToString().PadLeft(2, '0'));
		}

		public static string FormatAniDBRating(double rat)
		{
			// the episode ratings from UDP are out of 1000, while the HTTP AP gives it out of 10
			rat /= 100;

			return String.Format("{0:0.00}", rat);

		}

		public static int? ProcessNullableInt(string sint)
		{
			if (string.IsNullOrEmpty(sint))
				return null;
			else
				return int.Parse(sint);
		}

		public static string RemoveInvalidFolderNameCharacters(string folderName)
		{
			string ret = folderName.Replace(@"*", "");
			ret = ret.Replace(@"|", "");
			ret = ret.Replace(@"\", "");
			ret = ret.Replace(@"/", "");
			ret = ret.Replace(@":", "");
			ret = ret.Replace(((Char)34).ToString(), ""); // double quote
			ret = ret.Replace(@">", "");
			ret = ret.Replace(@"<", "");
			ret = ret.Replace(@"?", "");

			return ret;
		}

		public static string GetSortName(string name)
		{
			string newName = name;
			if (newName.ToLower().StartsWith("the "))
				newName.Remove(0, 4);
			if (newName.ToLower().StartsWith("a "))
				newName.Remove(0, 2);

			return newName;
		}

		public static string GetOSInfo()
		{
			//Get Operating system information.
			OperatingSystem os = Environment.OSVersion;
			//Get version information about the os.
			Version vs = os.Version;

			//Variable to hold our return value
			string operatingSystem = "";

			if (os.Platform == PlatformID.Win32Windows)
			{
				//This is a pre-NT version of Windows
				switch (vs.Minor)
				{
					case 0:
						operatingSystem = "95";
						break;
					case 10:
						if (vs.Revision.ToString() == "2222A")
							operatingSystem = "98SE";
						else
							operatingSystem = "98";
						break;
					case 90:
						operatingSystem = "Me";
						break;
					default:
						break;
				}
			}
			else if (os.Platform == PlatformID.Win32NT)
			{
				switch (vs.Major)
				{
					case 3:
						operatingSystem = "NT 3.51";
						break;
					case 4:
						operatingSystem = "NT 4.0";
						break;
					case 5:
						if (vs.Minor == 0)
							operatingSystem = "2000";
						else
							operatingSystem = "XP";
						break;
					case 6:
						if (vs.Minor == 0)
							operatingSystem = "Vista";
						else
							operatingSystem = "7";
						break;
					default:
						break;
				}
			}
			//Make sure we actually got something in our OS check
			//We don't want to just return " Service Pack 2" or " 32-bit"
			//That information is useless without the OS version.
			if (operatingSystem != "")
			{
				//Got something.  Let's prepend "Windows" and get more info.
				operatingSystem = "Windows " + operatingSystem;
				//See if there's a service pack installed.
				if (os.ServicePack != "")
				{
					//Append it to the OS name.  i.e. "Windows XP Service Pack 3"
					operatingSystem += " " + os.ServicePack;
				}
				//Append the OS architecture.  i.e. "Windows XP Service Pack 3 32-bit"
				operatingSystem += " " + getOSArchitecture().ToString() + "-bit";
			}
			//Return the information we've gathered.
			return operatingSystem;
		}

		public static int getOSArchitecture()
		{
			//easiest way: Just check the Size property of IntPtr.
			return IntPtr.Size * 8;
		}

		#region PrettyFilesize
		[DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
		static extern long StrFormatByteSize(long fileSize,
		[MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

		public static string FormatByteSize(long fileSize)
		{
			StringBuilder sbBuffer = new StringBuilder(20);
			StrFormatByteSize(fileSize, sbBuffer, 20);
			return sbBuffer.ToString();
		}
		#endregion

		public static int GetVideoWidth(string videoResolution)
		{
			int videoWidth = 0;
			if (videoResolution.Trim().Length > 0)
			{
				string[] dimensions = videoResolution.Split('x');
				if (dimensions.Length > 0) int.TryParse(dimensions[0], out videoWidth);
			}
			return videoWidth;
		}

		public static int GetVideoHeight(string videoResolution)
		{
			int videoHeight = 0;
			if (videoResolution.Trim().Length > 0)
			{
				string[] dimensions = videoResolution.Split('x');
				if (dimensions.Length > 1) int.TryParse(dimensions[1], out videoHeight);
			}
			return videoHeight;
		}

		public static int GetVideoSourceRanking(string source)
		{
			if (source.ToUpper().Contains("BLU")) return 100;
			if (source.ToUpper().Contains("DVD")) return 75;
			if (source.ToUpper().Contains("HDTV")) return 50;
			if (source.ToUpper().Contains("DTV")) return 40;
			if (source.ToUpper().Trim() == "TV") return 30;
			if (source.ToUpper().Contains("VHS")) return 20;

			return 0;
		}

		public static int GetOverallVideoSourceRanking(string videoResolution, string source, int bitDepth)
		{
			int vidWidth = GetVideoWidth(videoResolution);
			int score = 0;
			score += GetVideoSourceRanking(source);
			score += bitDepth;

			if (vidWidth > 1900) score += 100;
			else if (vidWidth > 1300) score += 50;
			else if (vidWidth > 1100) score += 25;
			else if (vidWidth > 800) score += 10;
			else if (vidWidth > 700) score += 8;
			else if (vidWidth > 500) score += 7;
			else if (vidWidth > 400) score += 6;
			else if (vidWidth > 1300) score += 5;
			else score += 2;

			return score;
		}

		public static int GetScheduledHours(ScheduledUpdateFrequency freq)
		{
			switch (freq)
			{
				case ScheduledUpdateFrequency.Daily: return 24;
				case ScheduledUpdateFrequency.HoursSix: return 6;
				case ScheduledUpdateFrequency.HoursTwelve: return 12;
				case ScheduledUpdateFrequency.Never: return int.MaxValue;
			}

			return int.MaxValue;
		}
	}
}
