using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using NLog;

namespace AniDBAPI.Commands
{
	public class AniDBCommand_GetUpdated : AniDBUDPCommand, IAniDBUDPCommand
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public string StartTime { get; set; }
		public int AniDBEntity { get; set; }
		public int RecordCount { get; set; }
		public List<int> AnimeIDList { get; set; }
		public string AnimeIDListRaw { get; set; }

		// 243 UPDATED
		// 1|346|1279968638|1185,7703,7682,7307,7432,6399,1349,1149,4193,500,154,5106,1022,357,7683,6327,5725,7653,1429,1653,1656,6716,2418,7690,635,7691,7692,7693,7694,7695,7696,1661,7697,7705,5406,7698,7700,1666,6227,2548,4235,7701,2195,2196,4990,4454,3110,3482,7381,2546,2880,1821,2661,5492,7234,2826,5284,266,6049,6083,7708,4238,5674,4678,1072,2871,2290,1836,2297,891,1061,2183,494,1681,2559,757,6773,594,5044,416,2107,1907,1913,524,505,1025,1825,1692,2045,5248,3677,1563,942,5793,1949,3182,1530,1105,1920,2684,1651,3050,2744,5518,4927,2322,484,947,1936,1383,2019,2740,1356,1864,3304,2048,7412,3667,2786,3388,1829,2007,3396,1496,2816,144,2863,2635,1811,3605,1930,1542,3366,3368,7389,2728,5133,2068,1912,965,2560,2555,3092,2343,2078,3636,7236,950,2582,3305,617,7142,917,591,330,925,2288,2654,4054,210,3658,313,1959,7280,2866,2129,945,2624,7688,7687,7686,7685,7707,7706,6391,3425,7251,7278,6452,4730,7620,7621,7664,6143,7618,4761,7478,7719,7486,7597,7399,7720,7068,4860,2879,7712,943,7713,5016,7715


		public string GetKey()
		{
			return "AniDBCommand_GetUpdated";
		}

		public virtual enHelperActivityType GetStartEventType()
		{
			return enHelperActivityType.GettingUpdated;
		}

		public virtual enHelperActivityType Process(ref Socket soUDP,
			ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
		{

			AnimeIDList = new List<int>();
			RecordCount = 0;

			ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

			// handle 555 BANNED and 598 - UNKNOWN COMMAND
			if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
			if (ResponseCode == 555) return enHelperActivityType.Banned_555;

			if (errorOccurred) return enHelperActivityType.NoUpdates;

			//BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCalendar: Response: {0}", socketResponse);

			// Process Response
			string sMsgType = socketResponse.Substring(0, 3);


			switch (sMsgType)
			{
				case "243":
					{

						// remove the header info
						string[] sDetails = socketResponse.Substring(0).Split('\n');

						if (sDetails.Length > 1)
						{
							// first item will be the status command so ignore
							// only concerned with the second line

							string[] flds = sDetails[1].Substring(0).Split('|');
							AniDBEntity = int.Parse(flds[0]);
							RecordCount = int.Parse(flds[1]);
							StartTime = flds[2];
							AnimeIDListRaw = flds[3].Trim();
							string[] aids = AnimeIDListRaw.Split(',');
							foreach (string sid in aids)
							{
								AnimeIDList.Add(int.Parse(sid));
							}
						}

						return enHelperActivityType.GotUpdated;

					}
				case "343":
					{
						return enHelperActivityType.NoUpdates;
					}
				case "501":
					{
						return enHelperActivityType.LoginRequired;
					}
			}

			return enHelperActivityType.NoUpdates;

		}

		public AniDBCommand_GetUpdated()
		{
			commandType = enAniDBCommandType.GetUpdated;
			RecordCount = 0;
		}

		public void Init(string startTime)
		{
			RecordCount = 0;
			this.StartTime = startTime;

			commandText = string.Format("UPDATED entity=1&time={0}", this.StartTime);
			//commandText = "UPDATED entity=1&age=1";

			commandID = "UPDATED ";
		}
	}
}
