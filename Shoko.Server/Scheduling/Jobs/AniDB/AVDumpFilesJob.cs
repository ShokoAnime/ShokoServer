using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(2, 16)]
public class AVDumpFilesJob : BaseJob<AVDumpHelper.AVDumpSession>
{
    public Dictionary<int, string> Videos { get; set; }

    public override string Title => "AVDumping Files";
    public override string TypeName => "AVDump Files";
    public override Dictionary<string, object> Details =>
        Videos.Values.Select((value, index) => (index, value)).ToDictionary(a => a.index.ToString(), a => (object)a.value);

    public override Task<AVDumpHelper.AVDumpSession> Process()
    {
        var session = AVDumpHelper.DumpFiles(Videos, synchronous: true);

        return Task.FromResult(session);
    }
}
