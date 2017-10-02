using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

using NutzCode.CloudFileSystem;
using Shoko.Commons.Extensions;
using Shoko.Commons.Notification;
using Shoko.Models.Azure;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

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
            ImportFolders = new ObservableCollection<SVR_ImportFolder>();
            CloudAccounts = new ObservableCollection<SVR_CloudAccount>();
            AdminMessages = new ObservableCollection<Azure_AdminMessage>();
            CloudProviders = new ObservableCollection<CloudProvider>();
            FolderProviders = new ObservableCollection<SVR_CloudAccount>();
        }

        private void Init()
        {
            //RefreshImportFolders();

            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent +=
                new Commands.CommandProcessorGeneral.QueueCountChangedHandler(
                    CmdProcessorGeneral_OnQueueCountChangedEvent);
            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent +=
                new Commands.CommandProcessorGeneral.QueueStateChangedHandler(
                    CmdProcessorGeneral_OnQueueStateChangedEvent);

            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent +=
                new Commands.CommandProcessorHasher.QueueCountChangedHandler(
                    CmdProcessorHasher_OnQueueCountChangedEvent);
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent +=
                new Commands.CommandProcessorHasher.QueueStateChangedHandler(
                    CmdProcessorHasher_OnQueueStateChangedEvent);

            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent +=
                new Commands.CommandProcessorImages.QueueCountChangedHandler(
                    CmdProcessorImages_OnQueueCountChangedEvent);
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent +=
                new Commands.CommandProcessorImages.QueueStateChangedHandler(
                    CmdProcessorImages_OnQueueStateChangedEvent);


            //Populate Cloud Providers
            foreach (ICloudPlugin plugin in CloudFileSystemPluginFactory.Instance.List)
            {
                if (plugin.Name != "Local File System")
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

        public ObservableCollection<Azure_AdminMessage> AdminMessages { get; set; }

        public void RefreshAdminMessages()
        {
            AdminMessages.Clear();

            try
            {
                List<Azure_AdminMessage> msgs = AzureWebAPI.Get_AdminMessages();
                if (msgs == null || msgs.Count == 0)
                {
                    AdminMessagesAvailable = false;
                    return;
                }

                foreach (Azure_AdminMessage msg in msgs)
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
            set { this.SetField(() => adminMessagesAvailable, value); }
        }

        private int hasherQueueCount = 0;

        public int HasherQueueCount
        {
            get { return hasherQueueCount; }
            set { this.SetField(() => hasherQueueCount, value); }
        }

        private string hasherQueueState = string.Empty;

        public string HasherQueueState
        {
            get { return hasherQueueState; }
            set { this.SetField(() => hasherQueueState, value); }
        }

        private int imagesQueueCount = 0;

        public int ImagesQueueCount
        {
            get { return imagesQueueCount; }
            set { this.SetField(() => imagesQueueCount, value); }
        }

        private string imagesQueueState = string.Empty;

        public string ImagesQueueState
        {
            get { return imagesQueueState; }
            set { this.SetField(() => imagesQueueState, value); }
        }

        private int generalQueueCount = 0;

        public int GeneralQueueCount
        {
            get { return generalQueueCount; }
            set { this.SetField(() => generalQueueCount, value); }
        }

        private string generalQueueState = string.Empty;

        public string GeneralQueueState
        {
            get { return generalQueueState; }
            set { this.SetField(() => generalQueueState, value); }
        }

        private bool hasherQueuePaused = false;

        public bool HasherQueuePaused
        {
            get { return hasherQueuePaused; }
            set { this.SetField(() => hasherQueuePaused, value); }
        }

        private bool hasherQueueRunning = true;

        public bool HasherQueueRunning
        {
            get { return hasherQueueRunning; }
            set { this.SetField(() => hasherQueueRunning, value); }
        }

        private bool generalQueuePaused = false;

        public bool GeneralQueuePaused
        {
            get { return generalQueuePaused; }
            set { this.SetField(() => generalQueuePaused, value); }
        }

        private bool generalQueueRunning = true;

        public bool GeneralQueueRunning
        {
            get { return generalQueueRunning; }
            set { this.SetField(() => generalQueueRunning, value); }
        }

        private bool imagesQueuePaused = false;

        public bool ImagesQueuePaused
        {
            get { return imagesQueuePaused; }
            set { this.SetField(() => imagesQueuePaused, value); }
        }

        private bool imagesQueueRunning = true;

        public bool ImagesQueueRunning
        {
            get { return imagesQueueRunning; }
            set { this.SetField(() => imagesQueueRunning, value); }
        }

        private string banReason = string.Empty;

        public string BanReason
        {
            get { return banReason; }
            set { this.SetField(() => banReason, value); }
        }

        private string banOrigin = string.Empty;

        public string BanOrigin
        {
            get { return banOrigin; }
            set { this.SetField(() => banOrigin, value); }
        }

        private bool isBanned = false;

        public bool IsBanned
        {
            get { return isBanned; }
            set { this.SetField(() => isBanned, value); }
        }

        private bool isInvalidSession = false;

        public bool IsInvalidSession
        {
            get { return isInvalidSession; }
            set { this.SetField(() => isInvalidSession, value); }
        }

        private bool waitingOnResponseAniDBUDP = false;

        public bool WaitingOnResponseAniDBUDP
        {
            get { return waitingOnResponseAniDBUDP; }
            set
            {
                this.SetField(() => waitingOnResponseAniDBUDP, value);
                NotWaitingOnResponseAniDBUDP = !value;
            }
        }

        private bool notWaitingOnResponseAniDBUDP = true;

        public bool NotWaitingOnResponseAniDBUDP
        {
            get { return notWaitingOnResponseAniDBUDP; }
            set { this.SetField(() => notWaitingOnResponseAniDBUDP, value); }
        }

        private string waitingOnResponseAniDBUDPString = Commons.Properties.Resources.Command_Idle;

        public string WaitingOnResponseAniDBUDPString
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                waitingOnResponseAniDBUDPString = Commons.Properties.Resources.Command_Idle;
                return waitingOnResponseAniDBUDPString;
            }
            set { this.SetField(() => waitingOnResponseAniDBUDPString, value); }
        }

        private string extendedPauseString = string.Empty;

        public string ExtendedPauseString
        {
            get { return extendedPauseString; }
            set { this.SetField(() => extendedPauseString, value); }
        }

        private bool hasExtendedPause = false;

        public bool HasExtendedPause
        {
            get { return hasExtendedPause; }
            set { this.SetField(() => hasExtendedPause, value); }
        }

        public ObservableCollection<SVR_ImportFolder> ImportFolders { get; set; }

        public ObservableCollection<SVR_CloudAccount> FolderProviders { get; set; }

        public ObservableCollection<CloudProvider> CloudProviders { get; set; }

        public class CloudProvider
        {
            public string Name { get; set; }
            public byte[] Bitmap { get; set; }
            public ICloudPlugin Plugin { get; set; }
        }


        public ObservableCollection<SVR_CloudAccount> CloudAccounts { get; set; }

        public void RefreshImportFolders()
        {
            ImportFolders.Clear();
            try
            {
                RepoFactory.ImportFolder.GetAll().ForEach(a => ImportFolders.Add(a));
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
                RepoFactory.CloudAccount.GetAll().ForEach(a => CloudAccounts.Add(a));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        public void RefreshFolderProviders()
        {
            FolderProviders.Clear();
            FolderProviders.Add(SVR_CloudAccount.CreateLocalFileSystemAccount());
            RepoFactory.CloudAccount.GetAll().ForEach(a => FolderProviders.Add(a));
        }

        #endregion
    }
}