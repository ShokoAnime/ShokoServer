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
using Shoko.Abstractions.Services;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Hashing;
using Shoko.Server.Models.Legacy;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Server;
using Shoko.Server.Services;

namespace Shoko.Server.Utilities;

public class Scanner
{
    // TODO this needs to be completely rewritten
    private readonly BackgroundWorker _workerIntegrityScanner = new();

    public static Scanner Instance { get; set; } = new();

    public int QueueCount { get; private set; }

    public Scanner()
    {
        _workerIntegrityScanner.WorkerReportsProgress = true;
        _workerIntegrityScanner.WorkerSupportsCancellation = true;
        _workerIntegrityScanner.DoWork += WorkerIntegrityScanner_DoWork;
    }

    public void Init()
    {
        MainThreadDispatch(() => { RepoFactory.Scan.GetAll().ForEach(a => Scans.Add(a)); });
        var runscan = Scans.FirstOrDefault(a => a.Status is ScanStatus.Running);
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
        _workerIntegrityScanner.RunWorkerAsync();
    }

    public void ClearScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        if (_workerIntegrityScanner.IsBusy && RunScan == ActiveScan)
        {
            CancelScan();
        }

        RepoFactory.ScanFile.Delete(RepoFactory.ScanFile.GetByScanID(ActiveScan.ScanID));
        RepoFactory.Scan.Delete(ActiveScan);
        MainThreadDispatch(() => { Scans.Remove(ActiveScan); });
        ActiveScan = null;
    }


    public void CancelScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        if (_workerIntegrityScanner.IsBusy)
        {
            cancelIntegrityCheck = true;
            while (_workerIntegrityScanner.IsBusy)
            {
                Thread.Sleep(100);
            }

            cancelIntegrityCheck = false;
        }
    }

    public bool Finished => (ActiveScan != null && ActiveScan.Status is ScanStatus.Finished) || ActiveScan == null;

    public string QueueState => ActiveScan != null
        ? ActiveScan.Status.ToString()
        : string.Empty;

    public bool QueuePaused => ActiveScan != null && ActiveScan.Status is ScanStatus.Standby;
    public bool QueueRunning => ActiveScan != null && ActiveScan.Status is ScanStatus.Running;
    public bool Exists => ActiveScan != null;
    private Scan activeScan;

    public Scan ActiveScan
    {
        get => activeScan;
        set
        {
            if (value != activeScan)
            {
                activeScan = value;
                Refresh();
                MainThreadDispatch(() =>
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
        if (activeScan != null)
        {
            QueueCount = RepoFactory.ScanFile.GetWaitingCount(activeScan.ScanID);
        }
    }

    public ObservableCollection<Scan> Scans { get; set; } = new();

    public ObservableCollection<ScanFile> ActiveErrorFiles { get; set; } = new();

    public bool HasFiles => Finished && ActiveErrorFiles.Count > 0;

    public void AddErrorScan(ScanFile file)
    {
        MainThreadDispatch(() =>
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
        var seriesToUpdate = new HashSet<AnimeSeries>();
        var vlpService = (VideoService)Utils.ServiceContainer.GetRequiredService<IVideoService>();
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

    internal Scan RunScan;

    private void WorkerIntegrityScanner_DoWork(object sender, DoWorkEventArgs e)
    {
        if (RunScan != null && RunScan.Status != ScanStatus.Finished)
        {
            var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
            scheduler.PauseAll();
            var s = RunScan;
            s.Status = ScanStatus.Running;
            RepoFactory.Scan.Save(s);
            Refresh();
            var files = RepoFactory.ScanFile.GetWaiting(s.ScanID);
            var cnt = 0;
            var hashingService = Utils.ServiceContainer.GetRequiredService<IVideoHashingService>();
            var hasher = hashingService.GetProviderInfo<CoreHashProvider>().Provider;
            foreach (var sf in files)
            {
                try
                {
                    if (!File.Exists(sf.FullName))
                    {
                        sf.Status = ScanFileStatus.ErrorFileNotFound;
                    }
                    else
                    {
                        var f = new FileInfo(sf.FullName);
                        if (sf.FileSize != f.Length)
                        {
                            sf.Status = ScanFileStatus.ErrorInvalidSize;
                        }
                        else
                        {
                            var hashes = hasher.GetHashesForVideo(new() { File = new(sf.FullName), EnabledHashTypes = new HashSet<string>() { "ED2K" } }).GetAwaiter().GetResult();
                            var ed2k = hashes.FirstOrDefault(a => a.Type is "ED2K")?.Value;
                            if (string.IsNullOrEmpty(ed2k))
                            {
                                sf.Status = ScanFileStatus.ErrorMissingHash;
                            }
                            else
                            {
                                sf.HashResult = ed2k;
                                if (!sf.Hash.Equals(ed2k, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sf.Status = ScanFileStatus.ErrorInvalidHash;
                                }
                                else
                                {
                                    sf.Status = ScanFileStatus.ProcessedOK;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    sf.Status = ScanFileStatus.ErrorIOError;
                }

                cnt++;
                sf.CheckDate = DateTime.Now;
                RepoFactory.ScanFile.Save(sf);
                if (sf.Status > ScanFileStatus.ProcessedOK)
                {
                    Instance.AddErrorScan(sf);
                }

                Refresh();

                if (cancelIntegrityCheck)
                {
                    break;
                }
            }

            if (files.Any(a => a.Status == (int)ScanFileStatus.Waiting))
            {
                s.Status = (int)ScanStatus.Standby;
            }
            else
            {
                s.Status = ScanStatus.Finished;
            }

            RepoFactory.Scan.Save(s);
            Refresh();
            RunScan = null;
        }
    }

    private delegate void DispatchHandler(Action a);

    private static event DispatchHandler OnDispatch;

    private static void MainThreadDispatch(Action a)
    {
        if (OnDispatch != null)
        {
            OnDispatch?.Invoke(a);
        }
        else
        {
            a();
        }
    }
}
