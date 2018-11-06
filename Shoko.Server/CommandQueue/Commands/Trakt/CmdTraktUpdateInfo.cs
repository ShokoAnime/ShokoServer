using System;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TraktTV;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{
    public class CmdTraktUpdateInfo : BaseCommand<CmdTraktUpdateInfo>, ICommand
    {
        public string TraktID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.Trakt.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;

        public string Id => $"TraktUpdateInfo_{TraktID}";

        public  QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.UpdateTraktData,
            extraParams = new[] {TraktID}
        };

        public WorkTypes WorkType => WorkTypes.Trakt;

        public CmdTraktUpdateInfo(string traktidorjson) 
        {
            //Little hack, if json desererialize, if not is the traktid
            string trimmed = traktidorjson.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}") && trimmed.Contains("TraktID"))
                JsonConvert.PopulateObject(traktidorjson, this, JsonSettings);
            else
                TraktID = traktidorjson;
        }



        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktUpdateInfo: {0}", TraktID);

            try
            {
                InitProgress(progress);
                TraktTVHelper.UpdateAllInfo(TraktID);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing CommandRequest_TraktUpdateInfo: {TraktID} - {ex}", ex);
            }
        }



    }
}