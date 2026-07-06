using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(1, 16)]
[LongRunning]
public class AVDumpFilesJob() : BaseJob<AVDumpHelper.AVDumpSession>
{
    /// <summary>
    /// Videos to dump.
    /// </summary>
    public Dictionary<int, string> Videos { get; set; } = [];

    /// <summary>
    /// Hash key representing the videos to dump.
    /// </summary>
    [JobKeyMember]
    public string Key
    {
        get => Videos is not null
            ? Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Videos.OrderBy(a => (a.Key, a.Value)).ToDictionary()))))
            : string.Empty;
        set { }
    }

    public override string Title => "AVDumping Files";

    public override string TypeName => "AVDump Files";

    public override Dictionary<string, object> Details => Videos.Count switch
    {
        1 => new()
        {
            { "FileID", Videos.First().Key },
            { "FilePath", Videos.First().Value },
        },
        > 1 => new()
        {
            { "Total Files", Videos.Count.ToString() + " files" },
            { "First FileID", Videos.First().Key },
            { "Last FileID", Videos.Last().Key },
        },
        _ => [],
    };

    public override Task<AVDumpHelper.AVDumpSession> Process()
    {
        var session = AVDumpHelper.DumpFiles(Videos, synchronous: true);

        return Task.FromResult(session);
    }
}
