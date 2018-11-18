using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Extensions;
using Shoko.Commons.Notification;
using Shoko.Commons.Properties;
using Shoko.Server.CommandQueue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using System.Reactive.Linq;
using Shoko.Commons.Queue;
using Shoko.Models.WebCache;
using Shoko.Server.CommandQueue.Commands;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
#pragma warning disable 649

namespace Shoko.Server
{
    public class ServerInfo : INotifyPropertyChangedExt
    {
        private static ServerInfo _instance;

        public static ServerInfo Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ServerInfo();
                    _instance.Init();
                }

                return _instance;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }


        private ServerInfo()
        {
            ImportFolders = new AsyncObservableCollection<SVR_ImportFolder>();
            CloudAccounts = new AsyncObservableCollection<SVR_CloudAccount>();
            CloudProviders = new AsyncObservableCollection<CloudProvider>();
            FolderProviders = new AsyncObservableCollection<SVR_CloudAccount>();
        }

        private void Init()
        {
            //RefreshImportFolders();

            Queue.Instance.Subscribe((cmd) =>
            {
                if (cmd.WorkType == WorkTypes.Image)
                {
                    ImagesQueueState = cmd;
                }
                else if (cmd.WorkType == WorkTypes.Hashing)
                {
                    HasherQueueState = cmd;
                }
                else
                {
                    GeneralQueueState = cmd;
                }
            });

            Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe((_) =>
            {
                if (Repo.Instance != null)
                {
                    ImagesQueueCount = Queue.Instance.GetCommandCount(WorkTypes.AniDB);
                    HasherQueueCount = Queue.Instance.GetCommandCount(WorkTypes.Hashing);
                    GeneralQueueCount = Queue.Instance.GetCommandCount() - ImagesQueueCount - HasherQueueCount;
                }
            });

            //Populate Cloud Providers
            foreach (ICloudPlugin plugin in CloudFileSystemPluginFactory.Instance.List)
            {
                if (!plugin.Name.Equals("Local File System", StringComparison.InvariantCultureIgnoreCase))
                {
                    CloudProvider p = new CloudProvider
                    {
                        Bitmap = plugin.Icon,
                        Name = plugin.Name,
                        Plugin = plugin
                    };
                    CloudProviders.Add(p);
                }
            }
        }

        

        #region Observable Properties



        private int hasherQueueCount = 0;

        public int HasherQueueCount
        {
            get => hasherQueueCount;
            set => this.SetField(() => hasherQueueCount, value);
        }

        private ICommand hasherQueueState;

        public ICommand HasherQueueState
        {
            get => hasherQueueState;
            set => this.SetFieldForced(() => hasherQueueState, value);
        }

        private int imagesQueueCount = 0;

        public int ImagesQueueCount
        {
            get => imagesQueueCount;
            set => this.SetField(() => imagesQueueCount, value);
        }

        private ICommand imagesQueueState;

        public ICommand ImagesQueueState
        {
            get => imagesQueueState;
            set => this.SetFieldForced(() => imagesQueueState, value);
        }

        private int generalQueueCount = 0;

        public int GeneralQueueCount
        {
            get => generalQueueCount;
            set => this.SetField(() => generalQueueCount, value);
        }

        private ICommand generalQueueState;

        public ICommand GeneralQueueState
        {
            get => generalQueueState;
            set => this.SetFieldForced(() => generalQueueState, value);
        }

        private bool hasherQueuePaused = false;

        public bool HasherQueuePaused
        {
            get => hasherQueuePaused;
            set => this.SetField(() => hasherQueuePaused, value);
        }

        private bool hasherQueueRunning = true;

        public bool HasherQueueRunning
        {
            get => hasherQueueRunning;
            set => this.SetField(() => hasherQueueRunning, value);
        }

        private bool generalQueuePaused = false;

        public bool GeneralQueuePaused
        {
            get => generalQueuePaused;
            set => this.SetField(() => generalQueuePaused, value);
        }

        private bool generalQueueRunning = true;

        public bool GeneralQueueRunning
        {
            get => generalQueueRunning;
            set => this.SetField(() => generalQueueRunning, value);
        }

        private bool imagesQueuePaused = false;

        public bool ImagesQueuePaused
        {
            get => imagesQueuePaused;
            set => this.SetField(() => imagesQueuePaused, value);
        }

        private bool imagesQueueRunning = true;

        public bool ImagesQueueRunning
        {
            get => imagesQueueRunning;
            set => this.SetField(() => imagesQueueRunning, value);
        }

        private string banReason = string.Empty;

        public string BanReason
        {
            get => banReason;
            set => this.SetField(() => banReason, value);
        }

        private string banOrigin = string.Empty;

        public string BanOrigin
        {
            get => banOrigin;
            set => this.SetField(() => banOrigin, value);
        }

        private bool isBanned = false;

        public bool IsBanned
        {
            get => isBanned;
            set => this.SetField(() => isBanned, value);
        }

        private bool isInvalidSession = false;

        public bool IsInvalidSession
        {
            get => isInvalidSession;
            set => this.SetField(() => isInvalidSession, value);
        }

        private bool waitingOnResponseAniDBUDP = false;

        public bool WaitingOnResponseAniDBUDP
        {
            get => waitingOnResponseAniDBUDP;
            set
            {
                this.SetField(() => waitingOnResponseAniDBUDP, value);
                NotWaitingOnResponseAniDBUDP = !value;
            }
        }

        private bool notWaitingOnResponseAniDBUDP = true;

        public bool NotWaitingOnResponseAniDBUDP
        {
            get => notWaitingOnResponseAniDBUDP;
            set => this.SetField(() => notWaitingOnResponseAniDBUDP, value);
        }

        private string waitingOnResponseAniDBUDPString = Resources.Command_Idle;

        public string WaitingOnResponseAniDBUDPString
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

                waitingOnResponseAniDBUDPString = Resources.Command_Idle;
                return waitingOnResponseAniDBUDPString;
            }
            set => this.SetField(() => waitingOnResponseAniDBUDPString, value);
        }

        private string extendedPauseString = string.Empty;

        public string ExtendedPauseString
        {
            get => extendedPauseString;
            set => this.SetField(() => extendedPauseString, value);
        }

        private bool hasExtendedPause = false;

        public bool HasExtendedPause
        {
            get => hasExtendedPause;
            set => this.SetField(() => hasExtendedPause, value);
        }

        public AsyncObservableCollection<SVR_ImportFolder> ImportFolders { get; set; }

        public AsyncObservableCollection<SVR_CloudAccount> FolderProviders { get; set; }

        public AsyncObservableCollection<CloudProvider> CloudProviders { get; set; }

        public class CloudProvider
        {
            public string Name { get; set; }
            public byte[] Bitmap { get; set; }
            public ICloudPlugin Plugin { get; set; }
        }


        public AsyncObservableCollection<SVR_CloudAccount> CloudAccounts { get; set; }

        public void RefreshImportFolders()
        {
            try
            {
                ImportFolders.Clear();
                Repo.Instance.ImportFolder.GetAll().ForEach(a => ImportFolders.Add(a));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        public void RefreshCloudAccounts()
        {
            try
            {
                CloudAccounts.Clear();
                Repo.Instance.CloudAccount.GetAll().ForEach(a => CloudAccounts.Add(a));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        public void RefreshFolderProviders()
        {
            try
            {
                FolderProviders.Clear();
                FolderProviders.Add(SVR_CloudAccount.CreateLocalFileSystemAccount());
                Repo.Instance.CloudAccount.GetAll().ForEach(a => FolderProviders.Add(a));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        #endregion
    }
}