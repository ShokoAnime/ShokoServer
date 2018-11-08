using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Queue;
using Shoko.Models.WebCache;
using Shoko.Server.CommandQueue.Commands.WebCache;


// ReSharper disable HeuristicUnreachableCode

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBGetTitles : BaseCommand, ICommand
    { 
        public QueueStateStruct PrettyDescription => new QueueStateStruct { QueueState = QueueStateEnum.AniDB_GetTitles, ExtraParams = new string[0] };
        public WorkTypes WorkType => WorkTypes.AniDB;
        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 10;
        public string Id => $"_GetAniDBTitles_{_creation}";
        private readonly DateTime _creation=DateTime.Now;
        public override string Serialize() => string.Empty;

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_GetAniDBTitles");
            try
            {
                bool process = false;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!process) return;

                string url = Constants.AniDBTitlesURL;
                logger.Trace("Get AniDB Titles: {0}", url);
                ReportInit(progress);
                Stream s = Misc.DownloadWebBinary(url);
                ReportUpdate(progress,30);
                int bytes = 2048;
                byte[] data = new byte[bytes]; //USE OF BYTES LENGTH VALUES FOR DATA SIZE
                StringBuilder b = new StringBuilder();
                UTF8Encoding enc = new UTF8Encoding();

                GZipStream zis = new GZipStream(s, CompressionMode.Decompress);

                while ((bytes = zis.Read(data, 0, data.Length)) > 0)
                    b.Append(enc.GetString(data, 0, bytes));

                zis.Close();


                string[] lines = b.ToString().Split('\n');
                Dictionary<int, WebCache_AnimeIDTitle> titles = new Dictionary<int, WebCache_AnimeIDTitle>();
                foreach (string line in lines)
                {
                    if (line.Trim().Length == 0 || line.Trim().Substring(0, 1) == "#") continue;

                    string[] fields = line.Split('|');

                    int.TryParse(fields[0], out int animeID);
                    if (animeID == 0) continue;

                    string titleType = fields[1].Trim().ToLower();
                    //string language = fields[2].Trim().ToLower();
                    string titleValue = fields[3].Trim();


                    WebCache_AnimeIDTitle thisTitle;
                    if (titles.ContainsKey(animeID))
                    {
                        thisTitle = titles[animeID];
                    }
                    else
                    {
                        thisTitle = new WebCache_AnimeIDTitle {AnimeIDTitleId = 0, MainTitle = titleValue, AnimeID = animeID};
                        titles[animeID] = thisTitle;
                    }

                    if (!string.IsNullOrEmpty(thisTitle.Titles))
                        thisTitle.Titles += "|";

                    if (titleType.Equals("1"))
                        thisTitle.MainTitle = titleValue;

                    thisTitle.Titles += titleValue;
                }

                ReportUpdate(progress,60);

                foreach (WebCache_AnimeIDTitle aniTitle in titles.Values)
                {
                    Queue.Instance.Add(new CmdWebCacheSendAnimeTitle(aniTitle.AnimeID, aniTitle.MainTitle, aniTitle.Titles));
                }

                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDB.GetAniDBTitles: {ex}", ex);
            }
        }
    }
}