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
using System.Threading;

namespace JMMServer
{
	public static class Utils
	{
		public const int LastYear = 2050;

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		extern static bool IsWow64Process(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool isWow64);
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		extern static IntPtr GetCurrentProcess();
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		extern static IntPtr GetModuleHandle(string moduleName);
		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		extern static IntPtr GetProcAddress(IntPtr hModule, string methodName);

		private static Logger logger = LogManager.GetCurrentClassLogger();
        
        //Remove in .NET 4.0
        public static void CopyTo(this Stream input, Stream output, int bufferSize=0x1000)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, bytesRead);
            }
        }
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

		public static int GetAniDBDateAsSeconds(string dateXML, bool isStartDate)
		{
			// eg "2008-12-31" or "2008-12" or "2008"
			if (dateXML.Trim().Length < 4) return 0;

			string month = "1";
			string day = "1";

			string year = dateXML.Trim().Substring(0, 4);

			if (dateXML.Trim().Length > 4)
				month = dateXML.Trim().Substring(5, 2);
			else
			{
				if (isStartDate)
					month = "1";
				else
					month = "12";
			}

			if (dateXML.Trim().Length > 7)
				day = dateXML.Trim().Substring(8, 2);
			else
			{
				if (isStartDate)
					day = "1";
				else
				{
					// find the last day of the month
					int numberOfDays = DateTime.DaysInMonth(int.Parse(year), int.Parse(month));
					day = numberOfDays.ToString();
				}
			}

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

        public static long GetCurrentUTCTime()
        {
            DateTime dt = DateTime.Now.ToUniversalTime();
            TimeSpan sp = dt.Subtract(new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)sp.TotalSeconds;
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
		    while (ret.EndsWith("."))
		        ret = ret.Substring(0, ret.Length - 1);
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
                        switch (vs.Minor)
                        {
                            case 0: operatingSystem = "Vista / 2008 Server"; break;
                            case 1: operatingSystem = "7 / 2008 Server R2"; break;
                            case 2: operatingSystem = "8 / 2012 Server"; break;
                            case 3: operatingSystem = "8.1 / 2012 Server R2"; break;
                            default: operatingSystem = "Unknown"; break;
                        }
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

        public static string GetMd5Hash(string input)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                return GetMd5Hash(md5Hash, input);
            }
        }

        public static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }

		public static int getOSArchitecture()
		{
			if (Is64BitOperatingSystem)
				return 64;
			else
				return 32;
		}

		public static bool Is64BitProcess
		{
			get { return IntPtr.Size == 8; }
		}

		public static bool Is64BitOperatingSystem
		{
			get
			{
				// Clearly if this is a 64-bit process we must be on a 64-bit OS.
				if (Is64BitProcess)
					return true;
				// Ok, so we are a 32-bit process, but is the OS 64-bit?
				// If we are running under Wow64 than the OS is 64-bit.
				bool isWow64;
				return ModuleContainsFunction("kernel32.dll", "IsWow64Process") && IsWow64Process(GetCurrentProcess(), out isWow64) && isWow64;
			}
		}

		static bool ModuleContainsFunction(string moduleName, string methodName)
		{
			IntPtr hModule = GetModuleHandle(moduleName);
			if (hModule != IntPtr.Zero)
				return GetProcAddress(hModule, methodName) != IntPtr.Zero;
			return false;
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


		public static List<string> GetPossibleSubtitleFiles(string fileName)
		{
			List<string> subtileFiles = new List<string>();
			subtileFiles.Add(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName) + ".srt"));
			subtileFiles.Add(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName) + ".ass"));
			subtileFiles.Add(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName) + ".ssa"));
			subtileFiles.Add(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName) + ".idx"));
			subtileFiles.Add(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName) + ".sub"));

			return subtileFiles;
		}

		/// <summary>
		/// This method attempts to take a video resolution, and return something that is closer to a standard
		/// </summary>
		/// <param name="res"></param>
		/// <returns></returns>
		public static string GetStandardisedVideoResolution(string res)
		{
			double width = (double)GetVideoWidth(res);
			double height = (double)GetVideoHeight(res);

			if (width <= 0 || height <= 0) return res;

			/*
			 * ~16x9 
			640x360
			720x400
			720x480
			848x480
			1280x720
			1920x1080

			 * ~ 4x3
			640x480
			1280x960
			1024x576
			*/

			if (VideoResolutionWithFivePercent(width, height, 640, 360)) return "640x360";
			if (VideoResolutionWithFivePercent(width, height, 720, 400)) return "720x400";
			if (VideoResolutionWithFivePercent(width, height, 720, 480)) return "720x480";
			if (VideoResolutionWithFivePercent(width, height, 848, 480)) return "848x480";
			if (VideoResolutionWithFivePercent(width, height, 1280, 720)) return "1280x720";
			if (VideoResolutionWithFivePercent(width, height, 1920, 1080)) return "1920x1080";

			if (VideoResolutionWithFivePercent(width, height, 640, 480)) return "640x480";
			if (VideoResolutionWithFivePercent(width, height, 1280, 960)) return "1280x960";
			if (VideoResolutionWithFivePercent(width, height, 1024, 576)) return "1024x576";

			return res;

		}

		private static bool VideoResolutionWithFivePercent(double width, double height, int testWidth, int testHeight)
		{
			// get %5 differentials
			double widthLower = width * (double)0.95;
			double widthUpper = width * (double)1.05;

			double heightLower = height * (double)0.95;
			double heightUpper = height * (double)1.05;

			if (testWidth >= widthLower && testWidth <= widthUpper && testHeight >= heightLower && testHeight <= heightUpper)
				return true;
			else
				return false;
		}

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

		/*public static void GetFilesForImportFolder(string folderLocation, ref List<string> fileList)
		{
			if (Directory.Exists(folderLocation))
			{
				// get root level files
				fileList.AddRange(Directory.GetFiles(folderLocation, "*.*", SearchOption.TopDirectoryOnly));

				// search sub folders
				foreach (string dirName in Directory.GetDirectories(folderLocation))
				{
					try
					{
						if (dirName.ToUpper().Contains("RECYCLE.BIN")) continue;

						fileList.AddRange(Directory.GetFiles(dirName, "*.*", SearchOption.AllDirectories));
					}
					catch (Exception ex)
					{
						logger.Warn("Error accessing: {0} - {1}", dirName, ex.Message);
					}
				}
			}
		}*/

		public static void GetFilesForImportFolder(string sDir, ref List<string> fileList)
		{
			try
			{
				// get root level files
				fileList.AddRange(Directory.GetFiles(sDir, "*.*", SearchOption.TopDirectoryOnly));

				// search sub folders
				foreach (string d in Directory.GetDirectories(sDir))
				{
					DirectoryInfo di = new DirectoryInfo(d);
					bool isSystem = (di.Attributes & FileAttributes.System) == FileAttributes.System;
					if (isSystem) 
						continue;

					//fileList.AddRange(Directory.GetFiles(d, "*.*", SearchOption.TopDirectoryOnly));

					GetFilesForImportFolder(d, ref fileList);
				}
			}
			catch (System.Exception excpt)
			{
				Console.WriteLine(excpt.Message);
			}
		}

		public static bool StartStreamingVideo(string ipAddress, string fileName, string vidBitRate, string fps, string resWidth, string audioBitRate, string audioSamplerate, string port,
			ref string errorMessage, ref string streamingUri)
		{
			try
			{
				// the ipAddress should be passed by the calling client
				// this is because the client needs to know how to address the stream
				// for example, in JMM Desktop it will know the JMM Server location by either machine name or IP Address
				// it should pass this address in

				// REQUIRES VLC 2.0.2 or better

				// VLC cannot handle FLAC audio - a buf was submitted for this

				errorMessage = "";
				streamingUri = string.Format("http://{0}:{1}", ipAddress, port);

				string encoderOptions = "vcodec=h264,vb=1768,venc=x264{profile=baseline,preset=faster,no-cabac,trellis=0,keyint=50},deinterlace=-1,aenc=ffmpeg{aac-profile=low},acodec=mp4a,ab=512,samplerate=48000,channels=2,audio-sync";
				string subtitleTranscoder = "soverlay";
				//string muxerOptions = ":standard{access=file,mux=ts,dst=#OUT#}";
				string muxerOptions = ":standard{access=file,mux=ts,dst=8088}";

				string sout = "#transcode{" + encoderOptions + "," + subtitleTranscoder;
				//if (!Context.Profile.TranscoderParameters.ContainsKey("noResize") || Context.Profile.TranscoderParameters["noResize"] != "yes")
				//	sout += ",width=" + Context.OutputSize.Width + ",height=" + Context.OutputSize.Height;
				sout += "}" + muxerOptions;


				//string vlcStartTemplate = @" -v {0} --sout=#transcode%vcodec=WMV2,vb={1},fps={2},width={3},acodec=wma2,ab={4},channels=1,samplerate={5},soverlay+:http%mux=asf,dst=:{6}/+ --no-sout-rtp-sap --no-sout-standard-sap --sout-all --ttl=1 --sout-keep --sout-transcode-high-priority --sub-language=en";
				string vlcStartTemplate = @" -v {0} --ffmpeg-hw --sout-ffmpeg-strict=-2 --sout={1}";

				string vlcStop = @"/F /IM vlc.exe";
				//string vlcStart = string.Format(vlcStartTemplate, fileName, vidBitRate, fps, resWidth, audioBitRate, audioSamplerate, port);
				string vlcStart = string.Format(vlcStartTemplate, fileName, sout);
				vlcStart = vlcStart.Replace("%", "{");
				vlcStart = vlcStart.Replace("+", "}");

				Process.Start("taskkill", vlcStop);
				Thread.Sleep(1000);
				Process.Start(ServerSettings.VLCLocation, vlcStart);

				return true;
			}
			catch (Exception ex)
			{
				errorMessage = ex.Message;

				logger.ErrorException(ex.ToString(), ex);
				return false;
			}
		}

		public static void ExecuteCommandSync(object command)
		{
			try
			{
				// create the ProcessStartInfo using "cmd" as the program to be run,
				// and "/c " as the parameters.
				// Incidentally, /c tells cmd that we want it to execute the command that follows,
				// and then exit.
				System.Diagnostics.ProcessStartInfo procStartInfo =
					new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

				// The following commands are needed to redirect the standard output.
				// This means that it will be redirected to the Process.StandardOutput StreamReader.
				procStartInfo.RedirectStandardOutput = true;
				procStartInfo.UseShellExecute = false;
				// Do not create the black window.
				procStartInfo.CreateNoWindow = true;
				// Now we create a process, assign its ProcessStartInfo and start it
				System.Diagnostics.Process proc = new System.Diagnostics.Process();
				proc.StartInfo = procStartInfo;
				proc.Start();
				// Get the output into a string
				string result = proc.StandardOutput.ReadToEnd();
				// Display the command output.
				Console.WriteLine(result);
			}
			catch (Exception objException)
			{
				// Log the exception
			}
		}

		public static void ClearAutoUpdateCache()
		{
			// rmdir /s /q "%userprofile%\wc"
			ExecuteCommandSync("rmdir /s /q \"%userprofile%\\wc\"");
		}
	}
}
