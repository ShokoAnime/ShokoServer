using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using NutzCode.CloudFileSystem;

namespace JMMServer
{
    public class ServerInfo : INotifyPropertyChanged
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

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private ServerInfo()
        {
            ImportFolders = new ObservableCollection<ImportFolder>();
            CloudAccounts=new ObservableCollection<CloudAccount>();
            AdminMessages = new ObservableCollection<AdminMessage>();
            CloudProviders = new ObservableCollection<CloudProvider>();
            FolderProviders = new ObservableCollection<CloudAccount>();
        }

        private void Init()
        {
            //RefreshImportFolders();

            JMMService.CmdProcessorGeneral.OnQueueCountChangedEvent +=
                new Commands.CommandProcessorGeneral.QueueCountChangedHandler(
                    CmdProcessorGeneral_OnQueueCountChangedEvent);
            JMMService.CmdProcessorGeneral.OnQueueStateChangedEvent +=
                new Commands.CommandProcessorGeneral.QueueStateChangedHandler(
                    CmdProcessorGeneral_OnQueueStateChangedEvent);

            JMMService.CmdProcessorHasher.OnQueueCountChangedEvent +=
                new Commands.CommandProcessorHasher.QueueCountChangedHandler(CmdProcessorHasher_OnQueueCountChangedEvent);
            JMMService.CmdProcessorHasher.OnQueueStateChangedEvent +=
                new Commands.CommandProcessorHasher.QueueStateChangedHandler(CmdProcessorHasher_OnQueueStateChangedEvent);

            JMMService.CmdProcessorImages.OnQueueCountChangedEvent +=
                new Commands.CommandProcessorImages.QueueCountChangedHandler(CmdProcessorImages_OnQueueCountChangedEvent);
            JMMService.CmdProcessorImages.OnQueueStateChangedEvent +=
                new Commands.CommandProcessorImages.QueueStateChangedHandler(CmdProcessorImages_OnQueueStateChangedEvent);



            //Populate Cloud Providers
            foreach (ICloudPlugin plugin in CloudFileSystemPluginFactory.Instance.List)
            {
                if (plugin.Name != "Local File System")
                {
                    CloudProvider p = new CloudProvider
                    {
                        Icon = plugin.CreateIconImage(),
                        Name = plugin.Name,
                        Plugin = plugin
                    };
                    CloudProviders.Add(p);
                }
            }


        }

        void CmdProcessorImages_OnQueueStateChangedEvent(Commands.QueueStateEventArgs ev)
        {
            ImagesQueueState = ev.QueueState.formatMessage();
        }

        void CmdProcessorImages_OnQueueCountChangedEvent(Commands.QueueCountEventArgs ev)
        {
            ImagesQueueCount = ev.QueueCount;
        }

        void CmdProcessorHasher_OnQueueStateChangedEvent(Commands.QueueStateEventArgs ev)
        {
            HasherQueueState = ev.QueueState.formatMessage();
        }

        void CmdProcessorHasher_OnQueueCountChangedEvent(Commands.QueueCountEventArgs ev)
        {
            HasherQueueCount = ev.QueueCount;
        }

        void CmdProcessorGeneral_OnQueueStateChangedEvent(Commands.QueueStateEventArgs ev)
        {
            GeneralQueueState = ev.QueueState.formatMessage();
        }

        void CmdProcessorGeneral_OnQueueCountChangedEvent(Commands.QueueCountEventArgs ev)
        {
            GeneralQueueCount = ev.QueueCount;
        }

        #region Observable Properties

        public ObservableCollection<AdminMessage> AdminMessages { get; set; }

        public void RefreshAdminMessages()
        {
            AdminMessages.Clear();

            try
            {
                List<AdminMessage> msgs = AzureWebAPI.Get_AdminMessages();
                if (msgs == null || msgs.Count == 0)
                {
                    AdminMessagesAvailable = false;
                    return;
                }

                foreach (AdminMessage msg in msgs)
                    AdminMessages.Add(msg);

                AdminMessagesAvailable = true;
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private bool adminMessagesAvailable = false;

        public bool AdminMessagesAvailable
        {
            get { return adminMessagesAvailable; }
            set
            {
                adminMessagesAvailable = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AdminMessagesAvailable"));
            }
        }

        private int hasherQueueCount = 0;

        public int HasherQueueCount
        {
            get { return hasherQueueCount; }
            set
            {
                hasherQueueCount = value;
                OnPropertyChanged(new PropertyChangedEventArgs("HasherQueueCount"));
            }
        }

        private string hasherQueueState = "";

        public string HasherQueueState
        {
            get { return hasherQueueState; }
            set
            {
                hasherQueueState = value;
                OnPropertyChanged(new PropertyChangedEventArgs("HasherQueueState"));
            }
        }

        private int imagesQueueCount = 0;

        public int ImagesQueueCount
        {
            get { return imagesQueueCount; }
            set
            {
                imagesQueueCount = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ImagesQueueCount"));
            }
        }

        private string imagesQueueState = "";

        public string ImagesQueueState
        {
            get { return imagesQueueState; }
            set
            {
                imagesQueueState = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ImagesQueueState"));
            }
        }

        private int generalQueueCount = 0;

        public int GeneralQueueCount
        {
            get { return generalQueueCount; }
            set
            {
                generalQueueCount = value;
                OnPropertyChanged(new PropertyChangedEventArgs("GeneralQueueCount"));
            }
        }

        private string generalQueueState = "";

        public string GeneralQueueState
        {
            get { return generalQueueState; }
            set
            {
                generalQueueState = value;
                OnPropertyChanged(new PropertyChangedEventArgs("GeneralQueueState"));
            }
        }

        private bool hasherQueuePaused = false;

        public bool HasherQueuePaused
        {
            get { return hasherQueuePaused; }
            set
            {
                hasherQueuePaused = value;
                OnPropertyChanged(new PropertyChangedEventArgs("HasherQueuePaused"));
            }
        }

        private bool hasherQueueRunning = true;

        public bool HasherQueueRunning
        {
            get { return hasherQueueRunning; }
            set
            {
                hasherQueueRunning = value;
                OnPropertyChanged(new PropertyChangedEventArgs("HasherQueueRunning"));
            }
        }

        private bool generalQueuePaused = false;

        public bool GeneralQueuePaused
        {
            get { return generalQueuePaused; }
            set
            {
                generalQueuePaused = value;
                OnPropertyChanged(new PropertyChangedEventArgs("GeneralQueuePaused"));
            }
        }

        private bool generalQueueRunning = true;

        public bool GeneralQueueRunning
        {
            get { return generalQueueRunning; }
            set
            {
                generalQueueRunning = value;
                OnPropertyChanged(new PropertyChangedEventArgs("GeneralQueueRunning"));
            }
        }

        private bool imagesQueuePaused = false;

        public bool ImagesQueuePaused
        {
            get { return imagesQueuePaused; }
            set
            {
                imagesQueuePaused = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ImagesQueuePaused"));
            }
        }

        private bool imagesQueueRunning = true;

        public bool ImagesQueueRunning
        {
            get { return imagesQueueRunning; }
            set
            {
                imagesQueueRunning = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ImagesQueueRunning"));
            }
        }

        private string banReason = "";

        public string BanReason
        {
            get { return banReason; }
            set
            {
                banReason = value;
                OnPropertyChanged(new PropertyChangedEventArgs("BanReason"));
            }
        }

        private string banOrigin = "";

        public string BanOrigin
        {
            get { return banOrigin; }
            set
            {
                banOrigin = value;
                OnPropertyChanged(new PropertyChangedEventArgs("BanOrigin"));
            }
        }

        private bool isBanned = false;

        public bool IsBanned
        {
            get { return isBanned; }
            set
            {
                isBanned = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsBanned"));
            }
        }

        private bool isInvalidSession = false;

        public bool IsInvalidSession
        {
            get { return isInvalidSession; }
            set
            {
                isInvalidSession = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsInvalidSession"));
            }
        }

        private bool waitingOnResponseAniDBUDP = false;

        public bool WaitingOnResponseAniDBUDP
        {
            get { return waitingOnResponseAniDBUDP; }
            set
            {
                waitingOnResponseAniDBUDP = value;
                NotWaitingOnResponseAniDBUDP = !value;
                OnPropertyChanged(new PropertyChangedEventArgs("WaitingOnResponseAniDBUDP"));
            }
        }

        private bool notWaitingOnResponseAniDBUDP = true;

        public bool NotWaitingOnResponseAniDBUDP
        {
            get { return notWaitingOnResponseAniDBUDP; }
            set
            {
                notWaitingOnResponseAniDBUDP = value;
                OnPropertyChanged(new PropertyChangedEventArgs("NotWaitingOnResponseAniDBUDP"));
            }
        }

        private string waitingOnResponseAniDBUDPString = JMMServer.Properties.Resources.Command_Idle;

        public string WaitingOnResponseAniDBUDPString
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                waitingOnResponseAniDBUDPString = JMMServer.Properties.Resources.Command_Idle;
                return waitingOnResponseAniDBUDPString;
            }
            set
            {
                waitingOnResponseAniDBUDPString = value;
                OnPropertyChanged(new PropertyChangedEventArgs("WaitingOnResponseAniDBUDPString"));
            }
        }

        private string extendedPauseString = "";

        public string ExtendedPauseString
        {
            get { return extendedPauseString; }
            set
            {
                extendedPauseString = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ExtendedPauseString"));
            }
        }

        private bool hasExtendedPause = false;

        public bool HasExtendedPause
        {
            get { return hasExtendedPause; }
            set
            {
                hasExtendedPause = value;
                OnPropertyChanged(new PropertyChangedEventArgs("HasExtendedPause"));
            }
        }

        public ObservableCollection<ImportFolder> ImportFolders { get; set; }

        public ObservableCollection<CloudAccount> FolderProviders { get; set; }

        public ObservableCollection<CloudProvider> CloudProviders { get; set; }

        public class CloudProvider
        {
            public string Name { get; set; }
            public ImageSource Icon { get; set; }
            public ICloudPlugin Plugin { get; set; }
        }


        public ObservableCollection<CloudAccount> CloudAccounts { get; set; }
        public void RefreshImportFolders()
        {
            ImportFolders.Clear();
            try
            {
                RepoFactory.ImportFolder.GetAll().ForEach(a=>ImportFolders.Add(a));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        public void RefreshCloudAccounts()
        {
            CloudAccounts.Clear();
            try
            {
                RepoFactory.CloudAccount.GetAll().ForEach(a=>CloudAccounts.Add(a));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        public void RefreshFolderProviders()
        {
            FolderProviders.Clear();
            CloudAccount lfs = new CloudAccount() {Name = "NA", Provider = "Local File System"};
            FolderProviders.Add(lfs);
            RepoFactory.CloudAccount.GetAll().ForEach(a => FolderProviders.Add(a));
        }
        #endregion
    }
}