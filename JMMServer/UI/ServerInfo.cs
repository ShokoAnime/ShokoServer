using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using JMMServer.Commands;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer
{
    public class ServerInfo : INotifyPropertyChanged
    {
        private static ServerInfo _instance;

        private ServerInfo()
        {
            ImportFolders = new ObservableCollection<ImportFolder>();
            AdminMessages = new ObservableCollection<AdminMessage>();
        }

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
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void Init()
        {
            //RefreshImportFolders();

            JMMService.CmdProcessorGeneral.OnQueueCountChangedEvent += CmdProcessorGeneral_OnQueueCountChangedEvent;
            JMMService.CmdProcessorGeneral.OnQueueStateChangedEvent += CmdProcessorGeneral_OnQueueStateChangedEvent;

            JMMService.CmdProcessorHasher.OnQueueCountChangedEvent += CmdProcessorHasher_OnQueueCountChangedEvent;
            JMMService.CmdProcessorHasher.OnQueueStateChangedEvent += CmdProcessorHasher_OnQueueStateChangedEvent;

            JMMService.CmdProcessorImages.OnQueueCountChangedEvent += CmdProcessorImages_OnQueueCountChangedEvent;
            JMMService.CmdProcessorImages.OnQueueStateChangedEvent += CmdProcessorImages_OnQueueStateChangedEvent;
        }

        private void CmdProcessorImages_OnQueueStateChangedEvent(QueueStateEventArgs ev)
        {
            ImagesQueueState = ev.QueueState;
        }

        private void CmdProcessorImages_OnQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            ImagesQueueCount = ev.QueueCount;
        }

        private void CmdProcessorHasher_OnQueueStateChangedEvent(QueueStateEventArgs ev)
        {
            HasherQueueState = ev.QueueState;
        }

        private void CmdProcessorHasher_OnQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            HasherQueueCount = ev.QueueCount;
        }

        private void CmdProcessorGeneral_OnQueueStateChangedEvent(QueueStateEventArgs ev)
        {
            GeneralQueueState = ev.QueueState;
        }

        private void CmdProcessorGeneral_OnQueueCountChangedEvent(QueueCountEventArgs ev)
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
                var msgs = AzureWebAPI.Get_AdminMessages();
                if (msgs == null || msgs.Count == 0)
                {
                    AdminMessagesAvailable = false;
                    return;
                }

                foreach (var msg in msgs)
                    AdminMessages.Add(msg);

                AdminMessagesAvailable = true;
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private bool adminMessagesAvailable;

        public bool AdminMessagesAvailable
        {
            get { return adminMessagesAvailable; }
            set
            {
                adminMessagesAvailable = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AdminMessagesAvailable"));
            }
        }

        private int hasherQueueCount;

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

        private int imagesQueueCount;

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

        private int generalQueueCount;

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

        private bool hasherQueuePaused;

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

        private bool generalQueuePaused;

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

        private bool imagesQueuePaused;

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

        private bool isBanned;

        public bool IsBanned
        {
            get { return isBanned; }
            set
            {
                isBanned = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsBanned"));
            }
        }

        private bool isInvalidSession;

        public bool IsInvalidSession
        {
            get { return isInvalidSession; }
            set
            {
                isInvalidSession = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsInvalidSession"));
            }
        }

        private bool waitingOnResponseAniDBUDP;

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

        private string waitingOnResponseAniDBUDPString = Resources.Command_Idle;

        public string WaitingOnResponseAniDBUDPString
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                waitingOnResponseAniDBUDPString = Resources.Command_Idle;
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

        private bool hasExtendedPause;

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

        public void RefreshImportFolders()
        {
            ImportFolders.Clear();

            try
            {
                var repFolders = new ImportFolderRepository();
                var fldrs = repFolders.GetAll();

                foreach (var ifolder in fldrs)
                    ImportFolders.Add(ifolder);
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        #endregion
    }
}