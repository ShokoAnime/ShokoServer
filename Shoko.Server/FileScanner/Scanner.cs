using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.CommandQueue;
using Shoko.Server.CommandQueue.Commands;
using Shoko.Server.CommandQueue.Commands.Hash;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.FileScanner
{
    public class Scanner
    {
        public const string BatchName = "Scan_";
        private static readonly Lazy<Scanner> _instance = new Lazy<Scanner>(() => new Scanner());
        public static Scanner Instance => _instance.Value;
        public AsyncObservableCollection<ScannerInfo> Scans { get; set; } = new AsyncObservableCollection<ScannerInfo>();

    

        public int GetQueueCount(Scan scan)
        {
            return Queue.Instance.GetCommandCount(BatchName + scan.ScanID);
        }

        public void ReAddErrorFile(ScanFile sf)
        {
            ScannerInfo info = Scans.FirstOrDefault(a => a.Scan.ScanID == sf.ScanID);
            if (info!=null && info.ErrorFiles.Any(a=>a.ScanFileID==sf.ScanFileID))
            { 
                ScannerInfo si = Scans.First(a => a.Scan.ScanID == sf.ScanID);
                SVR_VideoLocal_Place pl = Repo.Instance.VideoLocal_Place.GetByID(sf.VideoLocal_Place_ID);
                if (pl != null)
                {
                    info.ErrorFiles.Remove(sf);
                    Repo.Instance.ScanFile.Delete(sf);
                    Queue.Instance.Add(new CmdVerifyFile(pl),BatchName+si.Scan.ScanID);
                }
            }
        }
        public void ReAddAllFiles(ScannerInfo si)
        {
            if (si.ErrorFiles.Any(a => a.ScanID==si.Scan.ScanID))
            {
                List<ScanFile> files = si.ErrorFiles.ToList();
                List<int> acts = files.Select(a => a.VideoLocal_Place_ID).ToList();
                List<SVR_VideoLocal_Place> locals = Repo.Instance.VideoLocal_Place.GetMany(acts);
                if (locals.Count>0)
                {
                    si.ErrorFiles.Clear();
                    Repo.Instance.ScanFile.Delete(files);
                    Queue.Instance.AddRange(locals.Select(a=>new CmdVerifyFile(a)), BatchName + si.Scan.ScanID);
                }
            }
        }
        //This must be init before queue start
        public void Init()
        {
            List<Scan> scans;
            using (var upd = Repo.Instance.Scan.BeginBatchUpdate(Repo.Instance.Scan.GetAll))
            {
                foreach (Scan s in upd)
                {
                    int cnt = Queue.Instance.GetCommandCount(BatchName + s.ScanID);
                    int newstatus = cnt > 0 ? (int) ScanStatus.Running : (int) ScanStatus.Finish;
                    if (s.Status != newstatus)
                    {
                        s.Status = newstatus;
                        upd.Update(s);
                    }
                }

                scans = upd.Commit();
            }

            Queue.Instance.Where(a => a.Batch.StartsWith(BatchName)).Subscribe((cmd) =>
            {

                CmdVerifyFile vf = cmd as CmdVerifyFile;
                List<Expression<Func<object>>> changedProperties=new List<Expression<Func<object>>>();
                if (vf != null)
                {
                    string[] spl = vf.Batch.Split('_');
                    int scanid = int.Parse(spl[1]);
                    ScannerInfo info = Scans.First(a => a.Scan.ScanID == scanid);
                    if (vf.Status == CommandStatus.Finished || vf.Status == CommandStatus.Error && vf.Retries ==vf.MaxRetries)
                    {
                        if (vf.ScanFileStatus != ScanFileStatus.ProcessedOK)
                        {
                            ScanFile sf = new ScanFile();
                            sf.FullName = vf.FullName;
                            sf.ScanID = scanid;
                            sf.CheckDate = vf.CheckDate;
                            sf.FileSize = vf.OriginalSize;
                            sf.FileSizeResult = vf.VerifiedSize;
                            sf.Status = (int) vf.ScanFileStatus;
                            sf.Hash = vf.OriginalHash;
                            sf.HashResult = vf.VerifiedHash;
                            sf.ImportFolderID = vf.ImportFolderId;
                            sf.VideoLocal_Place_ID = vf.VideoLocalPlaceId;
                            Repo.Instance.ScanFile.BeginAdd(sf).Commit();
                            info.ErrorFiles.Add(sf);
                        }

                        int cnt = Queue.Instance.GetCommandCount(vf.Batch) - 1; //Current command didnt finish yet (thats why 1)
                        if (cnt <= 0)
                        {
                            using (var upd = Repo.Instance.Scan.BeginAddOrUpdate(scanid))
                            {
                                upd.Entity.Status = (int) ScanStatus.Finish;
                                upd.Commit();
                                changedProperties.AddRange(new Expression<Func<object>>[]{()=>info.CanBeCanceled,()=>info.CanBeResumed,()=>info.CanBePaused,()=>info.CanBeStarted});
                            }
                        }

                        info.Count = cnt;
                        changedProperties.Add(() => info.Count);
                    }

                    info.State = vf.PrettyDescription.FormatMessage() + " " + ((int) vf.Progress) + " %";
                    changedProperties.Add(()=>info.State);
                    info.OnPropertyChanged(changedProperties.ToArray());
                }
            });
            Scans = new AsyncObservableCollection<ScannerInfo>(scans.Select(a=>new ScannerInfo(a)));
            foreach(ScannerInfo s in Scans)
            {
                s.ErrorFiles = new AsyncObservableCollection<ScanFile>(Repo.Instance.ScanFile.GetByScanID(s.Scan.ScanID));
            }
        }

        public void Add(Scan s)
        {
            ScannerInfo sc=new ScannerInfo(s);
            Scans.Add(sc);
        }

        public void Destroy(ScannerInfo info)
        {
            Cancel(info);
            Repo.Instance.ScanFile.FindAndDelete(() => Repo.Instance.ScanFile.GetByScanID(info.Scan.ScanID));
            Scans.Remove(info);
            Repo.Instance.Scan.Delete(info.Scan);
        }
        public void Start(ScannerInfo info)
        {
            if (info.Scan.Status == (int) ScanStatus.Running || GetQueueCount(info.Scan) > 0)
                return;
            int id = info.Scan.ScanID;
            using (var upd = Repo.Instance.Scan.BeginAddOrUpdate(id))
            {
                upd.Entity.Status = (int) ScanStatus.Standby;
                upd.Commit();
            }

            HashSet<int> imp = new HashSet<int>(info.Scan.GetImportFolderList());
            List<SVR_VideoLocal> vl = imp.SelectMany(a => Repo.Instance.VideoLocal.GetByImportFolder(a)).Distinct().ToList();
            List<ICommand> cmds = new List<ICommand>();
            foreach (SVR_VideoLocal v in vl)
            {
                foreach (SVR_VideoLocal_Place p in v.Places.Where(a => imp.Contains(a.ImportFolderID)))
                {
                    cmds.Add(new CmdVerifyFile(p));
                }
            }

            using (var upd = Repo.Instance.Scan.BeginAddOrUpdate(id))
            {
                upd.Entity.Status = (int) ScanStatus.Running;
                upd.Commit();
            }

            Queue.Instance.AddRange(cmds,BatchName+id);
            info.Count = Queue.Instance.GetCommandCount(BatchName+id);
            info.OnPropertyChanged(() => info.Count, () => info.CanBeCanceled, () => info.CanBeResumed, () => info.CanBePaused, () => info.CanBeStarted);
        }

        public void Pause(ScannerInfo info)
        {
            if (info.Scan.Status != (int) ScanStatus.Running || GetQueueCount(info.Scan) == 0)
                return;
            Queue.Instance.PauseBatch(BatchName + info.Scan.ScanID);
            info.IsPaused = true;
            info.OnPropertyChanged(() => info.CanBeResumed, () => info.CanBePaused);
        }

        public void Resume(ScannerInfo info)
        {
            if (info.Scan.Status != (int) ScanStatus.Running || GetQueueCount(info.Scan) == 0)
                return;
            Queue.Instance.ResumeBatch(BatchName + info.Scan.ScanID);
            info.IsPaused = false;
            info.OnPropertyChanged(() => info.CanBeResumed, () => info.CanBePaused);
        }

        public void Cancel(ScannerInfo info)
        {
            int id = info.Scan.ScanID;
            Queue.Instance.ClearBatch(BatchName + id);
            using (var upd = Repo.Instance.Scan.BeginAddOrUpdate(id))
            {
                upd.Entity.Status = (int) ScanStatus.Standby;
                upd.Commit();
            }
            info.OnPropertyChanged(() => info.CanBeCanceled, () => info.CanBeResumed, () => info.CanBePaused, () => info.CanBeStarted);
        }
    }
}