using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.WebCache;
using Shoko.Server.CommandQueue.Commands.Hash;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerSyncHashes : BaseCommand, ICommand
    {
        public string ParallelTag { get; set; } =  WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 1;
        public string Id => "SyncHashes";

        public QueueStateStruct PrettyDescription { get; } = new QueueStateStruct {QueueState = QueueStateEnum.SyncHashes, ExtraParams = new string[0]};
        public string WorkType => WorkTypes.Server;

        public CmdServerSyncHashes(string str)
        {

        }

        public CmdServerSyncHashes()
        {

        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                List<SVR_VideoLocal> allfiles = Repo.Instance.VideoLocal.GetAll().ToList();
                List<SVR_VideoLocal> missfiles = allfiles.Where(
                        a =>
                            string.IsNullOrEmpty(a.CRC32) || string.IsNullOrEmpty(a.SHA1) ||
                            string.IsNullOrEmpty(a.MD5) || a.SHA1 == "0000000000000000000000000000000000000000" ||
                            a.MD5 == "00000000000000000000000000000000")
                    .ToList();
                ReportUpdate(progress,10);
                List<SVR_VideoLocal> withfiles = allfiles.Except(missfiles).ToList();
                Dictionary<int, (string ed2k, string crc32, string md5, string sha1)> updates = new Dictionary<int, (string ed2k, string crc32, string md5, string sha1)>();

                //Check if we can populate md5,sha and crc from AniDB_Files
                List<SVR_VideoLocal> vm = missfiles.ToList();
                for(int x=0;x<vm.Count;x++)
                {
                    double prog = (x + 1) * 60 / (double)missfiles.Count;
                    SVR_VideoLocal v = vm[x];
                    PrettyDescription.QueueState = QueueStateEnum.CheckingFile;
                    PrettyDescription.ExtraParams = new [] {v.Info};

                    SVR_AniDB_File file = Repo.Instance.AniDB_File.GetByHash(v.ED2KHash);
                    if (file != null)
                    {
                        if (!string.IsNullOrEmpty(file.CRC) && !string.IsNullOrEmpty(file.SHA1) &&
                            !string.IsNullOrEmpty(file.MD5))
                        {
                            updates[v.VideoLocalID] = (file.Hash, file.CRC, file.MD5, file.SHA1);
                            missfiles.Remove(v);
                            withfiles.Add(v);
                            ReportUpdate(progress,10+prog);
                            continue;
                        }
                    }
                   WebCache_FileHash ls = WebCacheAPI.Instance.GetHash(WebCache_HashType.ED2K, v.ED2KHash);
                    if (ls != null)
                    {
                        updates[v.VideoLocalID] = (ls.ED2K.ToUpperInvariant(), ls.CRC32.ToUpperInvariant(), ls.MD5.ToUpperInvariant(), ls.SHA1.ToUpperInvariant());
                        missfiles.Remove(v);
                    }
                    ReportUpdate(progress, 10 + prog);
                }

                //We need to recalculate the sha1, md5 and crc32 of the missing ones.
                List<ICommand> tohash=new List<ICommand>();
                foreach (SVR_VideoLocal v in missfiles)
                {
                    try
                    {
                        SVR_VideoLocal_Place p = v.GetBestVideoLocalPlace();
                        if (p != null && p.ImportFolder.CloudID == 0)
                            tohash.Add(new CmdHashFile(p.FullServerPath,true));
                    }
                    catch
                    {
                        //Ignored
                    }
                }
                Queue.Instance.AddRange(tohash);
                ReportUpdate(progress,80);
                if (updates.Count > 0)
                {
                    using (var upd = Repo.Instance.VideoLocal.BeginBatchUpdate(() => Repo.Instance.VideoLocal.GetMany(updates.Keys)))
                    {
                        foreach (SVR_VideoLocal v in upd)
                        {
                            (string ed2k, string crc32, string md5, string sha1) t = updates[v.VideoLocalID];
                            v.Hash = t.ed2k;
                            v.CRC32 = t.crc32;
                            v.MD5 = t.md5;
                            v.SHA1 = t.sha1;
                            upd.Update(v);
                        }
                        upd.Commit();
                    }
                }
                ReportUpdate(progress, 90);
                //Send the hashes
                WebCacheAPI.Instance.AddHashes(withfiles.Select(a=>a.ToHashRequest()));
                logger.Info("Sync Hashes Complete");
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing ServerSyncHashes: {ex}", ex);
            }
    }
    }
}
