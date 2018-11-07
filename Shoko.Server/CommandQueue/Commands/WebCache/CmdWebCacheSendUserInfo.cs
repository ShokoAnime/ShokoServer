using System;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.WebCache;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{

    public class CmdWebCacheSendUserInfo : BaseCommand, ICommand
    {
        public string Username { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendUserInfo_{Username}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SendAnonymousData,
            ExtraParams = new string[0]
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

       
        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                InitProgress(progress);
                WebCacheAPI.Send_UserInfo();
                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing WebCacheSendUserInfo {ex}", ex);
            }
        }        
    }
}