using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NLog;
using System.Diagnostics;
using JMMServer.Repositories;

namespace JMMServer
{
	public class RenameFileHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private static readonly char[] validTests = "AGEXQRTYDSC".ToCharArray();
		/* TESTS
		A	int     Anime id
		G	int     Group id
		E	text	Episode number
		X	text	Total number of episodes
		Q	text	Quality [unknown, very high, high, med, low, very low, corrupted, eyecancer]
		R	text	Rip source [unknown, camcorder, TV, DTV, VHS, VCD, SVCD, LD, DVD, HKDVD, www]
		T	text	Type [unknown, TV, OVA, Movie, Other, web]
		Y	int    	Year
		D	text	Dub language (one of the audio tracks) [japanese, english, ...]
		S	text	Sub language (one of the subtitle tracks) [japanese, english, ...]
		C	text	Codec (one of the audio/video tracks) [H264, XviD, MP3 CBR, ...]
		I	text	Tag has a value. Do not use %, i.e. I(eng) [eng, kan, rom, ...]
		 */

		/// <summary>
		/// Test if the file belongs to the specified anime
		/// </summary>
		/// <param name="test"></param>
		/// <param name="vid"></param>
		/// <returns></returns>
		public static bool EvaluateTestA(string test, VideoLocal vid)
		{
			try
			{
				bool notCondition = false;
				if (test.Substring(0, 1).Equals("!"))
				{
					notCondition = true;
					test = test.Substring(1, test.Length - 1);
				}

				int animeID = 0;
				int.TryParse(test, out animeID);

				if (vid.AniDBFile == null) return false;

				if (notCondition)
					return animeID != vid.AniDBFile.AnimeID;
				else
					return animeID == vid.AniDBFile.AnimeID;
				
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return false;
			}
		}

		/// <summary>
		/// Test if this file has the specified Dub (audio) language
		/// </summary>
		/// <param name="test"></param>
		/// <param name="vid"></param>
		/// <returns></returns>
		public static bool EvaluateTestD(string test, VideoLocal vid)
		{
			try
			{
				bool notCondition = false;
				if (test.Substring(0, 1).Equals("!"))
				{
					notCondition = true;
					test = test.Substring(1, test.Length - 1);
				}

				if (vid.AniDBFile == null) return false;

				if (notCondition)
				{
					foreach (Language lan in vid.AniDBFile.Languages)
					{
						if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase)) return false;
					}
					return true;
				}
				else
				{
					foreach (Language lan in vid.AniDBFile.Languages)
					{
						if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase)) return true;
					}
					return false;
				}

				
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return false;
			}
		}

		/// <summary>
		/// Test is this files has the specified Sub (subtitle) language
		/// </summary>
		/// <param name="test"></param>
		/// <param name="vid"></param>
		/// <returns></returns>
		public static bool EvaluateTestS(string test, VideoLocal vid)
		{
			try
			{
				bool notCondition = false;
				if (test.Substring(0, 1).Equals("!"))
				{
					notCondition = true;
					test = test.Substring(1, test.Length - 1);
				}

				if (vid.AniDBFile == null) return false;

				if (test.Trim().Equals(Constants.FileRenameReserved.None, StringComparison.InvariantCultureIgnoreCase) && vid.AniDBFile.Subtitles.Count == 0)
				{
					if (notCondition)
						return false;
					else
						return true;
				}

				if (notCondition)
				{
					foreach (Language lan in vid.AniDBFile.Subtitles)
					{
						if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase)) return false;
					}
					return true;
				}
				else
				{
					foreach (Language lan in vid.AniDBFile.Subtitles)
					{
						if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase)) return true;
					}
					return false;
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return false;
			}
		}


		/// <summary>
		/// Test whether the specified tag has a value
		/// </summary>
		/// <param name="test"></param>
		/// <param name="vid"></param>
		/// <param name="anime"></param>
		/// <returns></returns>
		public static bool EvaluateTestI(string test, VideoLocal vid, AniDB_Anime anime)
		{
			try
			{
				bool notCondition = false;
				if (test.Substring(0, 1).Equals("!"))
				{
					notCondition = true;
					test = test.Substring(1, test.Length - 1);
				}

				if (vid.AniDBFile == null) return false;
				if (anime == null) return false;

				if (test.Trim().Equals(Constants.FileRenameTag.AnimeID, StringComparison.InvariantCultureIgnoreCase))
					return true;

				#region Test if English title exists

				if (test.Trim().Equals(Constants.FileRenameTag.AnimeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
				{
					foreach (AniDB_Anime_Title ti in anime.Titles)
					{
						if (ti.Language.Equals(Constants.AniDBLanguageType.English, StringComparison.InvariantCultureIgnoreCase))
						{
							if (ti.TitleType.Trim().Equals(Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
								ti.TitleType.Trim().Equals(Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
							{
								if (notCondition) return false;
								else return true;
							}
						}

					}
					return false;
				}

				#endregion

				#region Test if Kanji title exists
				// Test if Kanji title exists
				if (test.Trim().Equals(Constants.FileRenameTag.AnimeNameKanji, StringComparison.InvariantCultureIgnoreCase))
				{
					foreach (AniDB_Anime_Title ti in anime.Titles)
					{
						if (ti.Language.Equals(Constants.AniDBLanguageType.Kanji, StringComparison.InvariantCultureIgnoreCase))
						{
							if (ti.TitleType.Trim().Equals(Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
								ti.TitleType.Trim().Equals(Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
							{
								if (notCondition) return false;
								else return true;
							}
						}

					}
					return false;
				}
				#endregion

				#region Test if Romaji title exists
				// Test if Romaji title exists
				if (test.Trim().Equals(Constants.FileRenameTag.AnimeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
				{
					foreach (AniDB_Anime_Title ti in anime.Titles)
					{
						if (ti.Language.Equals(Constants.AniDBLanguageType.Romaji, StringComparison.InvariantCultureIgnoreCase))
						{
							if (ti.TitleType.Trim().Equals(Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
								ti.TitleType.Trim().Equals(Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
							{
								if (notCondition) return false;
								else return true;
							}
						}

					}
					return false;
				}
				#endregion

				return false;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return false;
			}
		}

		public static void Test(VideoLocal vid)
		{
			/*string testScript = "IF A(69),A(1),A(2) DO FAIL" + Environment.NewLine + //Do not rename file if it is Naruto
				"DO ADD '%eng (%ann) - %enr - %epn '" + Environment.NewLine + //Add the base, same for all files
				"IF D(japanese);S(english) DO ADD '(SUB)'" + Environment.NewLine + //Add (SUB) if the file is subbed in english
				"IF D(japanese);S(none) DO ADD '(RAW)'" + Environment.NewLine + //Add (RAW) if the file is not subbed.
				"IF G(!unknown) DO ADD '[%grp]'" + Environment.NewLine + //Add group name if it is not unknown
				"DO ADD '(%CRC)'" + Environment.NewLine; //Always add crc
			*/
			//this would create the schema "%eng (%ann) - %enr - %epn (SUB)[%grp](%CRC)" for a normal subbed file.


			string testScript = "IF A(69),A(1),A(2) DO FAIL" + Environment.NewLine + //Do not rename file if it is Naruto
				"IF I(eng); DO ADD '%eng'" + Environment.NewLine + //Add (SUB) if the file is subbed in english
				"DO ADD '%eng (%ann) - %enr - %epn '" + Environment.NewLine + //Add the base, same for all files
				"IF D(japanese);S(english) DO ADD '(SUB)'" + Environment.NewLine + //Add (SUB) if the file is subbed in english
				"IF D(japanese);S(none) DO ADD '(RAW)'" + Environment.NewLine + //Add (RAW) if the file is not subbed.
				"IF G(!unknown) DO ADD '[%grp]'" + Environment.NewLine + //Add group name if it is not unknown
				"DO ADD '(%CRC)'" + Environment.NewLine; //Always add crc

			string newName = GetNewFileName(vid, testScript);
			Debug.WriteLine(newName);
		}

		private static string GetNewFileName(VideoLocal vid, string script)
		{
			string[] lines = script.Split(Environment.NewLine.ToCharArray());

			string newFileName = string.Empty;

			// get all the data so we don't need to get multiple times
			AniDB_File aniFile = vid.AniDBFile;
			if (aniFile == null) return string.Empty;

			List<AniDB_Episode> episodes = aniFile.Episodes;
			if (episodes.Count == 0) return string.Empty;

			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AniDB_Anime anime = repAnime.GetByAnimeID(episodes[0].AnimeID);
			if (anime == null) return string.Empty;

			foreach (string line in lines)
			{
				string thisLine = line.Trim();
				if (thisLine.Length == 0) continue;

				// check if this line has no tests (applied to all files)
				if (thisLine.StartsWith(Constants.FileRenameReserved.Do, StringComparison.InvariantCultureIgnoreCase))
				{
					string action = GetAction(thisLine);
					PerformActionOnFileName(ref newFileName, action, vid, aniFile, episodes, anime);
				}
				else
				{
					if (EvaluateTest(thisLine, vid, aniFile, episodes, anime))
					{
						Debug.WriteLine(string.Format("Line passed: {0}", thisLine));
						// if the line has passed the tests, then perform the action

						string action = GetAction(thisLine);

						// if the action is fail, we don't want to rename
						if (action.ToUpper().Trim().Equals(Constants.FileRenameReserved.Fail, StringComparison.InvariantCultureIgnoreCase))
							return string.Empty;

						PerformActionOnFileName(ref newFileName, action, vid, aniFile, episodes, anime);
					}
				}
			}

			return newFileName;
		}

		private static void PerformActionOnFileName(ref string newFileName, string action, VideoLocal vid, AniDB_File aniFile, List<AniDB_Episode> episodes, AniDB_Anime anime)
		{
			// find the first test
			int posStart = action.IndexOf(" ");
			if (posStart < 0) return;

			string actionType = action.Substring(0, posStart);
			string parameter = action.Substring(posStart + 1, action.Length - posStart - 1);

			// action is to add the the new file name
			if (action.ToUpper().Trim().Equals(Constants.FileRenameReserved.Add, StringComparison.InvariantCultureIgnoreCase))
				PerformActionOnFileNameADD(ref newFileName, action, vid, aniFile, episodes, anime);
		}

		private static void PerformActionOnFileNameADD(ref string newFileName, string action, VideoLocal vid, AniDB_File aniFile, List<AniDB_Episode> episodes, AniDB_Anime anime)
		{
			// check for double episodes

		}

		private static string GetAction(string line)
		{
			// find the first test
			int posStart = line.IndexOf("DO ");
			if (posStart < 0) return "";

			string action = line.Substring(posStart + 3, line.Length - posStart - 3);
			return action;
		}

		private static bool EvaluateTest(string line, VideoLocal vid, AniDB_File aniFile, List<AniDB_Episode> episodes, AniDB_Anime anime)
		{
			// determine if this line has a test
			foreach (char c in validTests)
			{
				string prefix = string.Format("IF {0}(", c);
				if (line.ToUpper().StartsWith(prefix))
				{
					// find the first test
					int posStart = line.IndexOf('(');
					int posEnd = line.IndexOf(')');
					int posStartOrig = posStart;

					if (posEnd < posStart) return false;

					string condition = line.Substring(posStart + 1, posEnd - posStart - 1);
					bool passed = EvaluateTest(c, condition, vid, aniFile, episodes, anime);

					// check for OR's and AND's
					bool foundAND= false;
					while (posStart > 0)
					{
						posStart = line.IndexOf(';', posStart);
						if (posStart > 0)
						{
							foundAND = true;
							string thisLineRemainder = line.Substring(posStart + 1, line.Length - posStart - 1).Trim(); // remove any spacing
							char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];

							int posStartNew = thisLineRemainder.IndexOf('(');
							int posEndNew = thisLineRemainder.IndexOf(')');
							condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

							bool thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

							if (!passed || !thisPassed) return false;

							posStart = posStart + 1;
						}
					}

					// if the first test passed, and we only have OR's left then it is an automatic success
					if (passed) return true;

					if (!foundAND)
					{
						posStart = posStartOrig;
						while (posStart > 0)
						{
							posStart = line.IndexOf(',', posStart);
							if (posStart > 0)
							{
								string thisLineRemainder = line.Substring(posStart + 1, line.Length - posStart - 1).Trim(); // remove any spacing
								char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];

								int posStartNew = thisLineRemainder.IndexOf('(');
								int posEndNew = thisLineRemainder.IndexOf(')');
								condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

								bool thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

								if (thisPassed) return true;

								posStart = posStart + 1;
							}
						}
					}
					
				}
			}

			return false;
		}

		private static bool EvaluateTest(char testChar, string testCondition, VideoLocal vid, AniDB_File aniFile, List<AniDB_Episode> episodes, AniDB_Anime anime)
		{
			switch (testChar)
			{
				case 'A': return EvaluateTestA(testCondition, vid);
				case 'D': return EvaluateTestD(testCondition, vid);
				case 'S': return EvaluateTestS(testCondition, vid);
				case 'I': return EvaluateTestI(testCondition, vid, anime);
			}

			return false;
		}
	}
}
