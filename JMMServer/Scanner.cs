using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JMMContracts;
using JMMServer.Entities;
using JMMServer.FileHelper;
using JMMServer.Repositories;

namespace JMMServer
{
    public class Scanner : INotifyPropertyChanged
    {
        private BackgroundWorker workerIntegrityScanner = new BackgroundWorker();

        public Scanner()
        {

            workerIntegrityScanner.WorkerReportsProgress = true;
            workerIntegrityScanner.WorkerSupportsCancellation = true;
            workerIntegrityScanner.DoWork += WorkerIntegrityScanner_DoWork;

        }

        public static Scanner Instance { get; set; } = new Scanner();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this,e);
        }

        private int queueCount = 0;

        public int QueueCount
        {
            get { return queueCount; }
            set
            {
                queueCount = value;
                OnPropertyChanged(new PropertyChangedEventArgs("QueueCount"));
            }
        }

        public void Init()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RepoFactory.Scan.GetAll().ForEach(a => Scans.Add(a));
            });
            Scan runscan = Scans.FirstOrDefault(a => a.ScanStatus == ScanStatus.Running);
            if (runscan != null)
            {
                ActiveScan = runscan;
                StartScan();
            }

        }

        public void StartScan()
        {
            if (ActiveScan == null)
                return;
            RunScan = ActiveScan;
            cancelIntegrityCheck = false;
            workerIntegrityScanner.RunWorkerAsync();
        }
        public void ClearScan()
        {
            if (ActiveScan == null)
                return;
            if (workerIntegrityScanner.IsBusy && RunScan==ActiveScan)
                CancelScan();
            RepoFactory.ScanFile.Delete(RepoFactory.ScanFile.GetByScanID(ActiveScan.ScanID));
            RepoFactory.Scan.Delete(ActiveScan);
            Application.Current.Dispatcher.Invoke(() =>
            {

                Scans.Remove(ActiveScan);
            });
            ActiveScan = null;
        }
        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;

            return null;
        }
        public void CancelScan()
        {
            if (ActiveScan == null)
                return;
            if (workerIntegrityScanner.IsBusy)
            {
                cancelIntegrityCheck = true;
                while(workerIntegrityScanner.IsBusy)
                {
                    DoEvents();
                    Thread.Sleep(100);
                }
                cancelIntegrityCheck = false;
            }
        }

        public bool Finished => (ActiveScan != null && ActiveScan.ScanStatus == ScanStatus.Finish) || ActiveScan==null;
        public string QueueState => ActiveScan != null ? ActiveScan.StatusText : string.Empty;
        public bool QueuePaused => ActiveScan != null && ActiveScan.ScanStatus == ScanStatus.Standby;
        public bool QueueRunning => ActiveScan!=null && ActiveScan.ScanStatus == ScanStatus.Running;
        public bool Exists => (ActiveScan != null);
        private Scan activeScan;
        public Scan ActiveScan
        {
            get
            {
                return activeScan;
            }
            set
            {
                if (value != activeScan)
                {
                    activeScan = value;
                    Refresh();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ActiveErrorFiles.Clear();
                        if (value != null)
                            RepoFactory.ScanFile.GetWithError(value.ScanID).ForEach(a => ActiveErrorFiles.Add(a));
                    });
                }
            }
        }

        public void Refresh()
        {
            OnPropertyChanged(new PropertyChangedEventArgs("Exists"));
            OnPropertyChanged(new PropertyChangedEventArgs("Finished"));
            OnPropertyChanged(new PropertyChangedEventArgs("QueueState"));
            OnPropertyChanged(new PropertyChangedEventArgs("QueuePaused"));
            OnPropertyChanged(new PropertyChangedEventArgs("QueueRunning"));
            if (activeScan!=null)
                QueueCount = RepoFactory.ScanFile.GetWaitingCount(activeScan.ScanID);
        }
        public ObservableCollection<Scan> Scans { get; set; }=new ObservableCollection<Scan>();

        public ObservableCollection<ScanFile> ActiveErrorFiles { get; set; }=new ObservableCollection<ScanFile>();

        public void AddErrorScan(ScanFile file)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ActiveScan != null && ActiveScan.ScanID == file.ScanID)
                    ActiveErrorFiles.Add(file);
            });
        }

        private bool cancelIntegrityCheck = false;
        internal Scan RunScan;

        public static int OnHashProgress(string fileName, int percentComplete)
        {
            return 1; //continue hashing (return 0 to abort)
        }
        private void WorkerIntegrityScanner_DoWork(object sender, DoWorkEventArgs e)
        {
            if (RunScan != null && RunScan.ScanStatus != ScanStatus.Finish)
            {
                Scan s = RunScan;
                s.Status = (int)ScanStatus.Running;
                RepoFactory.Scan.Save(s);
                Refresh();
                List<ScanFile> files = RepoFactory.ScanFile.GetWaiting(s.ScanID);
                int cnt = 0;
                foreach (ScanFile sf in files)
                {
                    try
                    {
                        if (!File.Exists(sf.FullName))
                            sf.Status = (int)ScanFileStatus.ErrorFileNotFound;
                        else
                        {
                            FileInfo f = new FileInfo(sf.FullName);
                            if (sf.FileSize != f.Length)
                                sf.Status = (int)ScanFileStatus.ErrorInvalidSize;
                            else
                            {
                                Hashes hashes = FileHashHelper.GetHashInfo(sf.FullName, true, OnHashProgress, false, false, false);
                                if (string.IsNullOrEmpty(hashes.ed2k))
                                {
                                    sf.Status = (int)ScanFileStatus.ErrorMissingHash;
                                }
                                else
                                {
                                    sf.HashResult = hashes.ed2k;
                                    if (!sf.Hash.Equals(sf.HashResult, StringComparison.InvariantCultureIgnoreCase))
                                        sf.Status = (int)ScanFileStatus.ErrorInvalidHash;
                                    else
                                        sf.Status = (int)ScanFileStatus.ProcessedOK;
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
                        Scanner.Instance.AddErrorScan(sf);
                    Refresh();

                    if (cancelIntegrityCheck)
                        break;
                }
                if (files.Any(a => a.ScanFileStatus == ScanFileStatus.Waiting))
                    s.Status = (int) ScanStatus.Standby;
                else
                    s.Status = (int) ScanStatus.Finish;
                RepoFactory.Scan.Save(s);
                Refresh();
                RunScan = null;
            }
        }


    }
}
