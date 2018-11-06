using System;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{

    public class CmdWebCacheSendUserInfo : BaseCommand<CmdWebCacheSendUserInfo>, ICommand
    {
        public string Username { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendUserInfo_{Username}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnonymousData,
            extraParams = new string[0]
        };

        public CmdWebCacheSendUserInfo(string usernameorjson)
        {
            //Little hack, if json desererialize, if not is the username
            string trimmed = usernameorjson.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}") && trimmed.Contains("Username"))
                JsonConvert.PopulateObject(usernameorjson, this, JsonSettings);
            else
                Username = usernameorjson;
        }

       
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                InitProgress(progress);
                AzureWebAPI.Send_UserInfo();
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheSendUserInfo {ex}", ex);
            }
        }        
    }
}