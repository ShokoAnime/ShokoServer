using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Utilities;

public class Scanner : INotifyPropertyChangedExt
{
    // TODO this needs to be completely rewritten
    private BackgroundWorker workerIntegrityScanner = new();

    public Scanner()
    {
        workerIntegrityScanner.WorkerReportsProgress = true;
        workerIntegrityScanner.WorkerSupportsCancellation = true;
        workerIntegrityScanner.DoWork += WorkerIntegrityScanner_DoWork;
    }

    public static Scanner Instance { get; set; } = new();

    public event PropertyChangedEventHandler PropertyChanged;

    public void NotifyPropertyChanged(string propname)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    }

    private int queueCount = 0;

    public int QueueCount
    {
        get => queueCount;
        set => this.SetField(() => queueCount, value);
    }

    public void Init()
    {
        Utils.MainThreadDispatch(() => { RepoFactory.Scan.GetAll().ForEach(a => Scans.Add(a)); });
        var runscan = Scans.FirstOrDefault(a => a.GetScanStatus() == ScanStatus.Running);
        if (runscan != null)
        {
            ActiveScan = runscan;
            StartScan();
        }
    }

    public void StartScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        RunScan = ActiveScan;
        cancelIntegrityCheck = false;
        workerIntegrityScanner.RunWorkerAsync();
    }

    public void ClearScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        if (workerIntegrityScanner.IsBusy && RunScan == ActiveScan)
        {
            CancelScan();
        }

        RepoFactory.ScanFile.Delete(RepoFactory.ScanFile.GetByScanID(ActiveScan.ScanID));
        RepoFactory.Scan.Delete(ActiveScan);
        Utils.MainThreadDispatch(() => { Scans.Remove(ActiveScan); });
        ActiveScan = null;
    }


    public void CancelScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        if (workerIntegrityScanner.IsBusy)
        {
            cancelIntegrityCheck = true;
            while (workerIntegrityScanner.IsBusy)
            {
                Utils.DoEvents();
                Thread.Sleep(100);
            }

            cancelIntegrityCheck = false;
        }
    }

    public bool Finished => (ActiveScan != null && ActiveScan.GetScanStatus() == ScanStatus.Finish) ||
                            ActiveScan == null;

    public string QueueState => ActiveScan != null ? ActiveScan.GetStatusText() : string.Empty;
    public bool QueuePaused => ActiveScan != null && ActiveScan.GetScanStatus() == ScanStatus.Standby;
    public bool QueueRunning => ActiveScan != null && ActiveScan.GetScanStatus() == ScanStatus.Running;
    public bool Exists => ActiveScan != null;
    private SVR_Scan activeScan;

    public SVR_Scan ActiveScan
    {
        get => activeScan;
        set
        {
            if (value != activeScan)
            {
                activeScan = value;
                Refresh();
                Utils.MainThreadDispatch(() =>
                {
                    ActiveErrorFiles.Clear();
                    if (value != null)
                    {
                        RepoFactory.ScanFile.GetWithError(value.ScanID).ForEach(a => ActiveErrorFiles.Add(a));
                    }
                });
            }
        }
    }

    public void Refresh()
    {
        this.OnPropertyChanged(() => Exists, () => Finished, () => QueueState, () => QueuePaused,
            () => QueueRunning);
        if (activeScan != null)
        {
            QueueCount = RepoFactory.ScanFile.GetWaitingCount(activeScan.ScanID);
        }
    }

    public ObservableCollection<SVR_Scan> Scans { get; set; } = new();

    public ObservableCollection<ScanFile> ActiveErrorFiles { get; set; } = new();

    public bool HasFiles => Finished && ActiveErrorFiles.Count > 0;

    public void AddErrorScan(ScanFile file)
    {
        Utils.MainThreadDispatch(() =>
        {
            if (ActiveScan != null && ActiveScan.ScanID == file.ScanID)
            {
                ActiveErrorFiles.Add(file);
            }
        });
    }

    public void DeleteAllErroredFiles()
    {
        if (ActiveScan == null)
        {
            return;
        }

        var files = ActiveErrorFiles.ToList();
        ActiveErrorFiles.Clear();
        var seriesToUpdate = new HashSet<SVR_AnimeSeries>();
        var vlpService = Utils.ServiceContainer.GetRequiredService<VideoLocal_PlaceService>();
        var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
        var databaseFactory = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>();
        using (var session = databaseFactory.SessionFactory.OpenSession())
        {
            files.ForEach(file =>
            {
                var place = RepoFactory.VideoLocalPlace.GetByID(file.VideoLocal_Place_ID);
                vlpService.RemoveAndDeleteFileWithOpenTransaction(session, place, seriesToUpdate).GetAwaiter().GetResult();
            });
            // update everything we modified
            Task.WhenAll(seriesToUpdate.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID))).GetAwaiter().GetResult();
        }

        RepoFactory.ScanFile.Delete(files);
    }

    private bool cancelIntegrityCheck;
    internal SVR_Scan RunScan;

    public static int OnHashProgress(string fileName, int percentComplete)
    {
        return 1; //continue hashing (return 0 to abort)
    }

    private void WorkerIntegrityScanner_DoWork(object sender, DoWorkEventArgs e)
    {
        if (RunScan != null && RunScan.GetScanStatus() != ScanStatus.Finish)
        {
            var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
            scheduler.PauseAll();
            var s = RunScan;
            s.Status = (int)ScanStatus.Running;
            RepoFactory.Scan.Save(s);
            Refresh();
            var files = RepoFactory.ScanFile.GetWaiting(s.ScanID);
            var cnt = 0;
            foreach (var sf in files)
            {
                try
                {
                    if (!File.Exists(sf.FullName))
                    {
                        sf.Status = (int)ScanFileStatus.ErrorFileNotFound;
                    }
                    else
                    {
                        var f = new FileInfo(sf.FullName);
                        if (sf.FileSize != f.Length)
                        {
                            sf.Status = (int)ScanFileStatus.ErrorInvalidSize;
                        }
                        else
                        {
                            var hashes =
                                FileHashHelper.GetHashInfo(sf.FullName, true, OnHashProgress, false, false, false);
                            if (string.IsNullOrEmpty(hashes.ED2K))
                            {
                                sf.Status = (int)ScanFileStatus.ErrorMissingHash;
                            }
                            else
                            {
                                sf.HashResult = hashes.ED2K;
                                if (!sf.Hash.Equals(sf.HashResult, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sf.Status = (int)ScanFileStatus.ErrorInvalidHash;
                                }
                                else
                                {
                                    sf.Status = (int)ScanFileStatus.ProcessedOK;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    sf.Status = (int)ScanFileStatus.ErrorIOError;
                }

                cnt++;
                sf.CheckDate = DateTime.Now;
                RepoFactory.ScanFile.Save(sf);
                if (sf.Status > (int)ScanFileStatus.ProcessedOK)
                {
                    Instance.AddErrorScan(sf);
                }

                Refresh();

                if (cancelIntegrityCheck)
                {
                    break;
                }
            }

            if (files.Any(a => a.GetScanFileStatus() == ScanFileStatus.Waiting))
            {
                s.Status = (int)ScanStatus.Standby;
            }
            else
            {
                s.Status = (int)ScanStatus.Finish;
            }

            RepoFactory.Scan.Save(s);
            Refresh();
            RunScan = null;
        }
    }
}
