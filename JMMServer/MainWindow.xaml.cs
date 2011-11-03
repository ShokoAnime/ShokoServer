using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Databases;
using NHibernate;
using JMMServer.Commands;
using JMMServer.Repositories;
using JMMServer.Entities;
using NLog;
using System.Threading;
using System.IO;
using JMMFileHelper;
using System.ServiceModel;
using System.ServiceModel.Description;
using JMMContracts;
using JMMServer.WebCache;
using System.ComponentModel;
using System.ServiceModel.Channels;
using System.Runtime.Serialization;
using System.Xml;
using JMMServer.AniDB_API.Commands;
using System.Windows;
using NLog.Config;
using System.Collections.ObjectModel;
using System.Windows.Input;
using JMMServer.Providers.TvDB;
using JMMServer.Providers.MovieDB;
using JMMServer.Providers.TraktTV;

namespace JMMServer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private System.Windows.Forms.NotifyIcon TippuTrayNotify;
		private System.Windows.Forms.ContextMenuStrip ctxTrayMenu;
		private bool isAppExiting = false;
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private static Uri baseAddress = new Uri("http://localhost:8111/JMMServer");
		private static Uri baseAddressImage = new Uri("http://localhost:8111/JMMServerImage");
		private static Uri baseAddressBinary = new Uri("http://localhost:8111/JMMServerBinary");
		private static Uri baseAddressTCP = new Uri("net.tcp://localhost:8112/JMMServerTCP");
		private static ServiceHost host = null;
		private static ServiceHost hostTCP = null;
		private static ServiceHost hostImage = null;
		private static ServiceHost hostBinary = null;

		private static BackgroundWorker workerImport = new BackgroundWorker();
		private static BackgroundWorker workerScanFolder = new BackgroundWorker();
		private static BackgroundWorker workerRemoveMissing = new BackgroundWorker();
		private static System.Timers.Timer autoUpdateTimer = null;
		private static System.Timers.Timer autoUpdateTimerShort = null;
		private static List<FileSystemWatcher> watcherVids = null;

		BackgroundWorker downloadImagesWorker = new BackgroundWorker();


		public MainWindow()
		{
			InitializeComponent();

			ConfigurationItemFactory.Default.Targets.RegisterDefinition("InternalCache", typeof(JMMServer.CachedNLogTarget));

			//Create an instance of the NotifyIcon Class
			TippuTrayNotify = new System.Windows.Forms.NotifyIcon();

			// This icon file needs to be in the bin folder of the application
			TippuTrayNotify = new System.Windows.Forms.NotifyIcon();
			Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/JMMServer;component/db.ico")).Stream;
			TippuTrayNotify.Icon = new System.Drawing.Icon(iconStream);
			iconStream.Dispose();

			//show the Tray Notify Icon
			TippuTrayNotify.Visible = true;

			CreateMenus();

			this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);
			this.StateChanged += new EventHandler(MainWindow_StateChanged);
			TippuTrayNotify.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(TippuTrayNotify_MouseDoubleClick);

			btnToolbarShutdown.Click += new RoutedEventHandler(btnToolbarShutdown_Click);
			btnHasherPause.Click += new RoutedEventHandler(btnHasherPause_Click);
			btnHasherResume.Click += new RoutedEventHandler(btnHasherResume_Click);
			btnGeneralPause.Click += new RoutedEventHandler(btnGeneralPause_Click);
			btnGeneralResume.Click += new RoutedEventHandler(btnGeneralResume_Click);
			btnImagesPause.Click += new RoutedEventHandler(btnImagesPause_Click);
			btnImagesResume.Click += new RoutedEventHandler(btnImagesResume_Click);

			btnRemoveMissingFiles.Click += new RoutedEventHandler(btnRemoveMissingFiles_Click);
			btnRunImport.Click += new RoutedEventHandler(btnRunImport_Click);
			btnSyncMyList.Click += new RoutedEventHandler(btnSyncMyList_Click);
			btnSyncVotes.Click += new RoutedEventHandler(btnSyncVotes_Click);
			btnUpdateTvDBInfo.Click += new RoutedEventHandler(btnUpdateTvDBInfo_Click);
			btnUpdateAllStats.Click += new RoutedEventHandler(btnUpdateAllStats_Click);
			btnSyncTrakt.Click += new RoutedEventHandler(btnSyncTrakt_Click);

			this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
			downloadImagesWorker.DoWork += new DoWorkEventHandler(downloadImagesWorker_DoWork);

			StartUp();
		}

		private void DownloadAllImages()
		{
			if (!downloadImagesWorker.IsBusy)
				downloadImagesWorker.RunWorkerAsync();
		}

		void downloadImagesWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			Importer.RunImport_GetImages();
		}

		void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			ServerInfo.Instance.RefreshImportFolders();

			//Importer.CheckForTvDBUpdates(true);
			//TvDBHelper.LinkAniDBTvDB(6751, 117851, 1, false);
			//JMMService.TvdbHelper.UpdateAllInfoAndImages(134181, true);

			//CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(7103, false);
			//cmd.ProcessCommand();

			//List<MovieDB_Movie_Result> movies = MovieDBHelper.Search("Naruto");
			//XMLService.Get_CrossRef_AniDB_Other(1, CrossRefType.MovieDB);
			//XMLService.Delete_CrossRef_AniDB_Other(1, CrossRefType.MovieDB);

			//CommandRequest_MovieDBSearchAnime cmd = new CommandRequest_MovieDBSearchAnime(5178, false);
			//cmd.ProcessCommand();

			//MovieDBHelper.ScanForMatches();

			//JMMServiceImplementation jmm = new JMMServiceImplementation();
			//jmm.GetNextUnwatchedEpisode(157);

			//AnimeSeriesRepository rep = new AnimeSeriesRepository();
			//AnimeSeries ser = rep.GetByAnimeID(8148);
			//ser.UpdateStats(true, true, true);

			//TraktTVHelper.GetShowInfo(117851);
			//TraktTVHelper.SearchShow("narutobummm");
			//TraktTVHelper.GetShowInfo("never find this");
			//CommandRequest_TraktSearchAnime cmd = new CommandRequest_TraktSearchAnime(8039, true);
			//cmd.ProcessCommand();

			//TraktTVHelper.ScanForMatches();

			//Trakt_ImageFanartRepository repFanart = new Trakt_ImageFanartRepository();
			//Trakt_ImageFanart fanart = repFanart.GetByShowIDAndSeason(5, 1);

			//Trakt_EpisodeRepository rep = new Trakt_EpisodeRepository();
			//Trakt_Episode ep = rep.GetByID(31);
			//Console.Write(ep.FullImagePath);

			//AnimeEpisodeRepository rep = new AnimeEpisodeRepository();
			//AnimeEpisode ep = rep.GetByAniDBEpisodeID(85084);
			//TraktTVHelper.MarkEpisodeWatched(ep);
			//JMMServiceImplementation imp = new JMMServiceImplementation();
			//imp.GetRecommendations(100, 1, 1);

			//CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(7656, true, false);
			//cmd.Save();

			//CommandRequest_GetCharactersCreators cmd = new CommandRequest_GetCharactersCreators(6751, false);
			//cmd.Save();
		}



		#region UI events and methods

		private void CommandBinding_ScanFolder(object sender, ExecutedRoutedEventArgs e)
		{
			object obj = e.Parameter;
			if (obj == null) return;

			try
			{
				if (obj.GetType() == typeof(ImportFolder))
				{
					ImportFolder fldr = (ImportFolder)obj;

					ScanFolder(fldr.ImportFolderID);
					MessageBox.Show("Process is Running", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
				}
			}
			catch (Exception ex)
			{
				Utils.ShowErrorMessage(ex);
			}

		}

		void btnUpdateTvDBInfo_Click(object sender, RoutedEventArgs e)
		{
			Importer.RunImport_UpdateTvDB(false);
			MessageBox.Show("Updates are queued", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void btnUpdateAllStats_Click(object sender, RoutedEventArgs e)
		{
			this.Cursor = Cursors.Wait;
			Importer.UpdateAllStats();
			this.Cursor = Cursors.Arrow;
			MessageBox.Show("Stats have been updated", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void btnSyncVotes_Click(object sender, RoutedEventArgs e)
		{
			CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
			cmdVotes.Save();
			MessageBox.Show("Command added to queue", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
			//JMMService.AnidbProcessor.IsBanned = true;
		}

		void btnSyncMyList_Click(object sender, RoutedEventArgs e)
		{
			SyncMyList();
			MessageBox.Show("Sync is Running", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void btnSyncTrakt_Click(object sender, RoutedEventArgs e)
		{
			CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(true);
			cmd.Save();
			MessageBox.Show("Sync is Queued", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void btnRunImport_Click(object sender, RoutedEventArgs e)
		{
			RunImport();
			MessageBox.Show("Import is Running", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void btnRemoveMissingFiles_Click(object sender, RoutedEventArgs e)
		{
			RemoveMissingFiles();
			MessageBox.Show("Process is Running", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void btnGeneralResume_Click(object sender, RoutedEventArgs e)
		{
			JMMService.CmdProcessorGeneral.Paused = false;
		}

		void btnGeneralPause_Click(object sender, RoutedEventArgs e)
		{
			JMMService.CmdProcessorGeneral.Paused = true;
		}

		void btnHasherResume_Click(object sender, RoutedEventArgs e)
		{
			JMMService.CmdProcessorHasher.Paused = false;
		}

		void btnHasherPause_Click(object sender, RoutedEventArgs e)
		{
			JMMService.CmdProcessorHasher.Paused = true;
		}

		void btnImagesResume_Click(object sender, RoutedEventArgs e)
		{
			JMMService.CmdProcessorImages.Paused = false;
		}

		void btnImagesPause_Click(object sender, RoutedEventArgs e)
		{
			JMMService.CmdProcessorImages.Paused = true;
		}

		void btnToolbarShutdown_Click(object sender, RoutedEventArgs e)
		{
			isAppExiting = true;
			this.Close();
			TippuTrayNotify.Visible = false;
			TippuTrayNotify.Dispose();
		}

		#endregion

		private void StartUp()
		{
			try
			{
				workerImport.WorkerReportsProgress = true;
				workerImport.WorkerSupportsCancellation = true;
				workerImport.DoWork += new DoWorkEventHandler(workerImport_DoWork);

				workerScanFolder.WorkerReportsProgress = true;
				workerScanFolder.WorkerSupportsCancellation = true;
				workerScanFolder.DoWork += new DoWorkEventHandler(workerScanFolder_DoWork);

				workerRemoveMissing.WorkerReportsProgress = true;
				workerRemoveMissing.WorkerSupportsCancellation = true;
				workerRemoveMissing.DoWork += new DoWorkEventHandler(workerRemoveMissing_DoWork);

				if (!DatabaseHelper.InitDB()) return;

				//init session factory
				ISessionFactory temp = JMMService.SessionFactory;

				SetupAniDBProcessor();
				StartHost();
				StartImageHost();
				StartBinaryHost();
				//StartTCPHost();

				//CreateImportFolders_Test();
				//CreateImportFolders2();

				//  Load all stats
				StatsCache.Instance.InitStats();

				JMMService.CmdProcessorGeneral.Init();
				JMMService.CmdProcessorHasher.Init();
				JMMService.CmdProcessorImages.Init();

				//AdhocRepository rep = new AdhocRepository();
				//Dictionary<int, string> dictStats = rep.GetAllVideoQualityByAnime();
				//Dictionary<int, string> vidq = rep.GetAllVideoQualityByGroup();
				//StatsCache.Instance.UpdateUsingGroup(7);

				//JMMService.TvdbHelper.SearchSeries("Imouto");
				//TvDB_Series tvser = TvDBHelper.GetSeriesInfoOnline(78857);
				

				/*CrossRef_AniDB_TvDB xref = new CrossRef_AniDB_TvDB();
				xref.AnimeID = 6107;
				xref.TvDBID = 85249;
				xref.TvDBSeasonNumber = 1;
				XMLService.Send_CrossRef_AniDB_TvDB(xref);*/

				//CrossRef_AniDB_TvDB xref = XMLService.Get_CrossRef_AniDB_TvDB(6107);

				#region Test Code

				//VideoLocalRepository repVids = new VideoLocalRepository();
				//VideoLocal vlocal = repVids.GetByID(1);
				//if (vlocal == null) return;

				//XMLService.Send_FileHash(vlocal);

				//XMLService.Get_FileHash(@"Zettai Shogeki - Platonic Heart\[Chihiro]_Zettai_Shogeki_~Platonic_Heart~_01v2_[h264][A057F3B2].mkv", 240600302);

				//CommandRequest_GetCalendar cmd = new CommandRequest_GetCalendar(true);
				//cmd.Save();

				//CommandRequest_GetReleaseGroup cmd = new CommandRequest_GetReleaseGroup(3938, true);
				//cmd.Save();

				//CommandRequest_SyncMyList cmd = new CommandRequest_SyncMyList(true);
				//cmd.ProcessCommand();

				//OMMService.AnidbProcessor.GetMyListFileStatus(838983);

				//CommandRequest_GetReleaseGroupStatus cmd = new CommandRequest_GetReleaseGroupStatus(7948, false);
				//cmd.Save();

				//CommandRequest_AddFileToMyList cmd = new CommandRequest_AddFileToMyList("8E451EA0C43FC94B51A8B638425D95BA");
				//cmd.Save();

				//CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(6313, false);
				//cmd.Save();

				//VideoLocalRepository repVids = new VideoLocalRepository();
				//VideoLocal vid = repVids.GetByHash("0866E8D605F4DA96222A529A066CC35E");
				//repVids.Delete(vid.VideoLocalID);
				//vid.ToggleWatchedStatus(true, true);



				//OMMServiceImplementation omm = new OMMServiceImplementation();
				//omm.GetAllSeries();

				//ReorganiseGundam();
				//HashTest();
				//HashTest2();
				//ReviewsTest();

				//CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(7671, true, false);
				//cmd.Save();

				//ProcessFileTest();
				//ProcessFiles();
				//ReadFiles();

				//CreateTestCommandRequests();
				//CreateSubGroupsTest();
				//UpdateStatsTest();

				//WebCacheTest();

				//OMMService.AnidbProcessor.Login();
				#endregion

				// timer for automatic updates
				autoUpdateTimer = new System.Timers.Timer();
				autoUpdateTimer.AutoReset = true;
				autoUpdateTimer.Interval = 10 * 60 * 1000; // 10 minutes * 60 seconds
				autoUpdateTimer.Elapsed += new System.Timers.ElapsedEventHandler(autoUpdateTimer_Elapsed);
				autoUpdateTimer.Start();

				// timer for automatic updates
				autoUpdateTimerShort = new System.Timers.Timer();
				autoUpdateTimerShort.AutoReset = true;
				autoUpdateTimerShort.Interval = 15 * 1000; // 15 seconds
				autoUpdateTimerShort.Elapsed += new System.Timers.ElapsedEventHandler(autoUpdateTimerShort_Elapsed);
				autoUpdateTimerShort.Start();

				StartWatchingFiles();

				DownloadAllImages();
				if (ServerSettings.RunImportOnStart) RunImport();


				//Console.WriteLine("Press ENTER to EXIT");
				//Console.ReadKey();

				
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
		}

		void autoUpdateTimerShort_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			JMMService.CmdProcessorImages.NotifyOfNewCommand();
		}

		#region Tray Minimize

		void MainWindow_StateChanged(object sender, EventArgs e)
		{
			if (this.WindowState == System.Windows.WindowState.Minimized) this.Hide();
		}

		void TippuTrayNotify_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			this.Show();
		}

		private void CreateMenus()
		{
			//Create a object for the context menu
			ctxTrayMenu = new System.Windows.Forms.ContextMenuStrip();

			//Add the Menu Item to the context menu
			System.Windows.Forms.ToolStripMenuItem mnuShow = new System.Windows.Forms.ToolStripMenuItem();
			mnuShow.Text = "Show";
			mnuShow.Click += new EventHandler(mnuShow_Click);
			ctxTrayMenu.Items.Add(mnuShow);

			//Add the Menu Item to the context menu
			System.Windows.Forms.ToolStripMenuItem mnuExit = new System.Windows.Forms.ToolStripMenuItem();
			mnuExit.Text = "Shut Down";
			mnuExit.Click += new EventHandler(mnuExit_Click);
			ctxTrayMenu.Items.Add(mnuExit);

			//Add the Context menu to the Notify Icon Object
			TippuTrayNotify.ContextMenuStrip = ctxTrayMenu;
		}

		void mnuShow_Click(object sender, EventArgs e)
		{
			this.Show();
		}

		private void ShutDown()
		{
			StopWatchingFiles();
			AniDBDispose();
			StopHost();
		}

		void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			//When the application is closed, check wether the application is 
			//exiting from menu or forms close button
			if (!isAppExiting)
			{
				//if the forms close button is triggered, cancel the event and hide the form
				//then show the notification ballon tip
				e.Cancel = true;
				this.Hide();
				TippuTrayNotify.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
				TippuTrayNotify.BalloonTipTitle = "Tippu Tray Notify";
				TippuTrayNotify.BalloonTipText = "Tippu Tray Notify has been minimized to the system tray. To open the application, double-click the icon in the system tray.";
				//TippuTrayNotify.ShowBalloonTip(400);
			}
			else
			{
				ShutDown();
			}
		}

		void mnuExit_Click(object sender, EventArgs e)
		{
			isAppExiting = true;
			this.Close();
			TippuTrayNotify.Visible = false;
			TippuTrayNotify.Dispose();
		}

		#endregion

		static void autoUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			Importer.CheckForCalendarUpdate(false);
			Importer.CheckForAnimeUpdate(false);
			Importer.CheckForTvDBUpdates(false);
			Importer.CheckForMyListSyncUpdate(false);
			Importer.CheckForTraktAllSeriesUpdate(false);
			Importer.CheckForTraktSyncUpdate(false);
		}

		public static void StartWatchingFiles()
		{
			if (!ServerSettings.WatchForNewFiles) return;

			StopWatchingFiles();

			watcherVids = new List<FileSystemWatcher>();

			ImportFolderRepository repNetShares = new ImportFolderRepository();
			foreach (ImportFolder share in repNetShares.GetAll())
			{
				try
				{
					if (Directory.Exists(share.ImportFolderLocation))
					{
						logger.Info("Watching ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
						FileSystemWatcher fsw = new FileSystemWatcher(share.ImportFolderLocation);
						fsw.IncludeSubdirectories = true;
						fsw.Created += new FileSystemEventHandler(fsw_Created);
						fsw.EnableRaisingEvents = true;
						watcherVids.Add(fsw);
					}
					else
					{
						logger.Error("ImportFolder not found for watching: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
					}
				}
				catch (Exception ex)
				{
					logger.ErrorException(ex.ToString(), ex);
				}
			}



			/*if (settings.DropFolder.Trim().Length > 0 && settings.DropFolderDestination.Trim().Length > 0)
			{
				FileSystemWatcher fsw = new FileSystemWatcher(settings.DropFolder);
				fsw.IncludeSubdirectories = true;
				fsw.Created += new FileSystemEventHandler(fsw_Created);
				fsw.EnableRaisingEvents = true;
				watcherVids.Add(fsw);
			}*/
		}

		public static void StopWatchingFiles()
		{
			if (watcherVids == null) return;

			foreach (FileSystemWatcher fsw in watcherVids)
			{
				fsw.EnableRaisingEvents = false;
			}
		}

		static void fsw_Created(object sender, FileSystemEventArgs e)
		{
			logger.Info("New file created: {0}: {1}", e.FullPath, e.ChangeType);
			if (e.ChangeType == WatcherChangeTypes.Created)
			{
				CommandRequest_HashFile cmd = new CommandRequest_HashFile(e.FullPath, false);
				cmd.Save();
			}
		}

		public static void ScanFolder(int importFolderID)
		{
			if (!workerScanFolder.IsBusy)
				workerScanFolder.RunWorkerAsync(importFolderID);
		}

		public static void RunImport()
		{
			if (!workerImport.IsBusy)
				workerImport.RunWorkerAsync();
		}

		public static void RemoveMissingFiles()
		{
			if (!workerRemoveMissing.IsBusy)
				workerRemoveMissing.RunWorkerAsync();
		}

		public static void SyncMyList()
		{
			Importer.CheckForMyListSyncUpdate(true);
		}

		static void workerRemoveMissing_DoWork(object sender, DoWorkEventArgs e)
		{
			try
			{
				Importer.RemoveRecordsWithoutPhysicalFiles();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.Message, ex);
			}
		}

		static void workerScanFolder_DoWork(object sender, DoWorkEventArgs e)
		{
			try
			{
				Importer.RunImport_ScanFolder(int.Parse(e.Argument.ToString()));

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.Message, ex);
			}
		}

		static void workerImport_DoWork(object sender, DoWorkEventArgs e)
		{

			try
			{
				Importer.RunImport_NewFiles();
				Importer.RunImport_IntegrityCheck();

				// TODO drop folder

				// TvDB association checks
				Importer.RunImport_ScanTvDB();

				// Trakt association checks
				Importer.RunImport_ScanTrakt();

				// MovieDB association checks
				Importer.RunImport_ScanMovieDB();

				// Check for missing images
				Importer.RunImport_GetImages();

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.Message, ex);
			}
		}

		private static void StartHost()
		{
			// Create the ServiceHost.
			host = new ServiceHost(typeof(JMMServiceImplementation), baseAddress);
			// Enable metadata publishing.
			ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
			smb.HttpGetEnabled = true;
			smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
			host.Description.Behaviors.Add(smb);

			BasicHttpBinding binding = new BasicHttpBinding();
			//binding.MessageEncoding = WSMessageEncoding.Mtom;
			binding.MaxReceivedMessageSize = 20971520;
			binding.MaxBufferPoolSize = 20971520; // 20 megabytes
			binding.MaxBufferSize = 20971520; // 20 megabytes
			binding.SendTimeout = new TimeSpan(0, 0, 30);

			host.AddServiceEndpoint(typeof(IJMMServer), binding, baseAddress);

			// Open the ServiceHost to start listening for messages. Since
			// no endpoints are explicitly configured, the runtime will create
			// one endpoint per base address for each service contract implemented
			// by the service.
			host.Open();
			logger.Trace("Now Accepting client connections...");
		}

		private static void StartTCPHost()
		{
			// Create the ServiceHost.
			hostTCP = new ServiceHost(typeof(JMMServiceImplementation), baseAddressTCP);
			// Enable metadata publishing.

			ServiceMetadataBehavior behavior = new ServiceMetadataBehavior();

			hostTCP.Description.Behaviors.Add(behavior);
			hostTCP.AddServiceEndpoint(typeof(IMetadataExchange), MetadataExchangeBindings.CreateMexNamedPipeBinding(), "net.pipe://localhost/service/mex/");


			NetTcpBinding binding = new NetTcpBinding();
			binding.ReceiveTimeout = new TimeSpan(30, 0, 30);
			binding.SendTimeout = new TimeSpan(30, 0, 30);
			binding.OpenTimeout = new TimeSpan(30, 0, 30);
			binding.CloseTimeout = new TimeSpan(30, 0, 30);
			binding.MaxBufferPoolSize = int.MaxValue;
			binding.MaxBufferSize = int.MaxValue;
			binding.MaxReceivedMessageSize = int.MaxValue;


			XmlDictionaryReaderQuotas quotas = new XmlDictionaryReaderQuotas();
			quotas.MaxArrayLength = int.MaxValue;
			quotas.MaxBytesPerRead = int.MaxValue;
			quotas.MaxDepth = int.MaxValue;
			quotas.MaxNameTableCharCount = int.MaxValue;
			quotas.MaxStringContentLength = int.MaxValue;

			binding.ReaderQuotas = quotas;

			hostTCP.AddServiceEndpoint(typeof(IJMMServer), binding, baseAddressTCP);



			hostTCP.Open();
			logger.Trace("Now Accepting client connections...");
		}

		private static void StartBinaryHost()
		{
			BinaryMessageEncodingBindingElement encoding = new BinaryMessageEncodingBindingElement();
			HttpTransportBindingElement transport = new HttpTransportBindingElement();
			Binding binding = new CustomBinding(encoding, transport);
			binding.Name = "BinaryBinding";


			//binding.MessageEncoding = WSMessageEncoding.Mtom;
			//binding.MaxReceivedMessageSize = 2147483647;


			// Create the ServiceHost.
			hostBinary = new ServiceHost(typeof(JMMServiceImplementation), baseAddressBinary);
			// Enable metadata publishing.
			ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
			smb.HttpGetEnabled = true;
			smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
			hostBinary.Description.Behaviors.Add(smb);

			hostBinary.AddServiceEndpoint(typeof(IJMMServer), binding, baseAddressBinary);

			// Open the ServiceHost to start listening for messages. Since
			// no endpoints are explicitly configured, the runtime will create
			// one endpoint per base address for each service contract implemented
			// by the service.
			hostBinary.Open();
			logger.Trace("Now Accepting client connections for test host...");
		}

		private static void StartImageHost()
		{
			BasicHttpBinding binding = new BasicHttpBinding();
			binding.MessageEncoding = WSMessageEncoding.Mtom;
			binding.MaxReceivedMessageSize = 2147483647;
			binding.Name = "httpLargeMessageStream";


			// Create the ServiceHost.
			hostImage = new ServiceHost(typeof(JMMServiceImplementationImage), baseAddressImage);
			// Enable metadata publishing.
			ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
			smb.HttpGetEnabled = true;
			smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
			hostImage.Description.Behaviors.Add(smb);

			hostImage.AddServiceEndpoint(typeof(IJMMServerImage), binding, baseAddressImage);

			// Open the ServiceHost to start listening for messages. Since
			// no endpoints are explicitly configured, the runtime will create
			// one endpoint per base address for each service contract implemented
			// by the service.
			hostImage.Open();
			logger.Trace("Now Accepting client connections for images...");
		}

		private static void ReadFiles()
		{
			// Steps for processing a file
			// 1. Check if it is a video file
			// 2. Check if we have a VideoLocal record for that file
			// .........

			// get a complete list of files
			List<string> fileList = new List<string>();
			ImportFolderRepository repNetShares = new ImportFolderRepository();
			foreach (ImportFolder share in repNetShares.GetAll())
			{
				logger.Debug("Import Folder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
				fileList.AddRange(Directory.GetFiles(share.ImportFolderLocation, "*.*", SearchOption.AllDirectories));
			}


			// get a list of all the shares we are looking at
			int filesFound = 0, videosFound = 0;
			int i = 0;

			// get a list of all files in the share
			foreach (string fileName in fileList)
			{
				i++;
				filesFound++;

				if (fileName.Contains("Sennou"))
				{
					logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);
				}

				if (!FileHashHelper.IsVideo(fileName)) continue;

				videosFound++;



			}
			logger.Debug("Found {0} files", filesFound);
			logger.Debug("Found {0} videos", videosFound);
		}

		private static void StopHost()
		{
			// Close the ServiceHost.
			//host.Close();

			if (hostImage != null)
				hostImage.Close();

			if (hostBinary != null)
				hostBinary.Close();
		}

		private static void SetupAniDBProcessor()
		{
			JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password, ServerSettings.AniDB_ServerAddress,
				ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);
		}

		private static void AniDBDispose()
		{
			logger.Info("Disposing...");
			if (JMMService.AnidbProcessor != null)
			{
				JMMService.AnidbProcessor.ForceLogout();
				JMMService.AnidbProcessor.Dispose();
				Thread.Sleep(1000);
			}

		}

		public static int OnHashProgress(string fileName, int percentComplete)
		{
			//string msg = Path.GetFileName(fileName);
			//if (msg.Length > 35) msg = msg.Substring(0, 35);
			//logger.Info("{0}% Hashing ({1})", percentComplete, Path.GetFileName(fileName));
			return 1; //continue hashing (return 0 to abort)
		}



		#region Tests

		private static void ReviewsTest()
		{
			CommandRequest_GetReviews cmd = new CommandRequest_GetReviews(7525, true);
			cmd.Save();

			//CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(7727, false);
			//cmd.Save();
		}

		private static void WebCacheTest()
		{
			string hash = "";
			hash = XMLService.Get_FileHash("Full Metal Panic! The Second Raid - S2 [AonE-AnY] (XviD) (704x396).avi", 181274624);
			hash = XMLService.Get_FileHash("Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv", 601722047);
			hash = XMLService.Get_FileHash("[Ayako]_Infinite_Stratos_-_IS_-_02_[H264][720p][05C376A9].mkv", 368502091);
		}

		private static void HashTest()
		{
			string fileName = @"C:\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";
			//string fileName = @"M:\[ Anime Test ]\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";

			DateTime start = DateTime.Now;
			Hashes hashes = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, false, false);
			TimeSpan ts = DateTime.Now - start;

			double doubleED2k = ts.TotalMilliseconds;

			start = DateTime.Now;
			Hashes hashes2 = Hasher.CalculateHashes(fileName, OnHashProgress, true, true, false, false);
			ts = DateTime.Now - start;

			double doubleCRC32 = ts.TotalMilliseconds;

			start = DateTime.Now;
			Hashes hashes3 = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, true, false);
			ts = DateTime.Now - start;

			double doubleMD5 = ts.TotalMilliseconds;

			start = DateTime.Now;
			Hashes hashes4 = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, false, true);
			ts = DateTime.Now - start;

			double doubleSHA1 = ts.TotalMilliseconds;

			start = DateTime.Now;
			Hashes hashes5 = Hasher.CalculateHashes(fileName, OnHashProgress, true, true, true, true);
			ts = DateTime.Now - start;

			double doubleAll = ts.TotalMilliseconds;

			logger.Info("ED2K only took {0} ms --- {1}/{2}/{3}/{4}", doubleED2k, hashes.ed2k, hashes.crc32, hashes.md5, hashes.sha1);
			logger.Info("ED2K + CRCR32 took {0} ms --- {1}/{2}/{3}/{4}", doubleCRC32, hashes2.ed2k, hashes2.crc32, hashes2.md5, hashes2.sha1);
			logger.Info("ED2K + MD5 took {0} ms --- {1}/{2}/{3}/{4}", doubleMD5, hashes3.ed2k, hashes3.crc32, hashes3.md5, hashes3.sha1);
			logger.Info("ED2K + SHA1 took {0} ms --- {1}/{2}/{3}/{4}", doubleSHA1, hashes4.ed2k, hashes4.crc32, hashes4.md5, hashes4.sha1);
			logger.Info("Everything took {0} ms --- {1}/{2}/{3}/{4}", doubleAll, hashes5.ed2k, hashes5.crc32, hashes5.md5, hashes5.sha1);
		}

		private static void HashTest2()
		{
			string fileName = @"C:\Anime\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";
			FileInfo fi = new FileInfo(fileName);
			string fileSize1 = Utils.FormatByteSize(fi.Length);
			DateTime start = DateTime.Now;
			Hashes hashes = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, false, false);
			TimeSpan ts = DateTime.Now - start;

			double doubleFile1 = ts.TotalMilliseconds;

			fileName = @"C:\Anime\[Coalgirls]_Bakemonogatari_01_(1280x720_Blu-Ray_FLAC)_[CA425D15].mkv";
			fi = new FileInfo(fileName);
			string fileSize2 = Utils.FormatByteSize(fi.Length);
			start = DateTime.Now;
			Hashes hashes2 = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, false, false);
			ts = DateTime.Now - start;

			double doubleFile2 = ts.TotalMilliseconds;


			fileName = @"C:\Anime\Highschool_of_the_Dead_Ep01_Spring_of_the_Dead_[1080p,BluRay,x264]_-_gg-THORA.mkv";
			fi = new FileInfo(fileName);
			string fileSize3 = Utils.FormatByteSize(fi.Length);
			start = DateTime.Now;
			Hashes hashes3 = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, false, false);
			ts = DateTime.Now - start;

			double doubleFile3 = ts.TotalMilliseconds;

			logger.Info("Hashed {0} in {1} ms --- {2}", fileSize1, doubleFile1, hashes.ed2k);
			logger.Info("Hashed {0} in {1} ms --- {2}", fileSize2, doubleFile2, hashes2.ed2k);
			logger.Info("Hashed {0} in {1} ms --- {2}", fileSize3, doubleFile3, hashes3.ed2k);

		}


		private static void UpdateStatsTest()
		{
			AnimeGroupRepository repGroups = new AnimeGroupRepository();
			foreach (AnimeGroup grp in repGroups.GetAllTopLevelGroups())
			{
				grp.UpdateStatsFromTopLevel(true, true);
			}
		}



		private static void CreateImportFolders_Test()
		{
			logger.Debug("Creating import folders...");
			ImportFolderRepository repImportFolders = new ImportFolderRepository();

			ImportFolder sn = repImportFolders.GetByImportLocation(@"M:\[ Anime Test ]");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderName = "Anime";
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderLocation = @"M:\[ Anime Test ]";
				repImportFolders.Save(sn);
			}

			logger.Debug("Complete!");
		}

		private static void ProcessFileTest()
		{
			//CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(@"M:\[ Anime Test ]\[HorribleSubs] Dragon Crisis! - 02 [720p].mkv", false);
			//CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(@"M:\[ Anime Test ]\[Doki] Saki - 01 (720x480 h264 DVD AAC) [DC73ACB9].mkv");
			//cr_hashfile.Save();

			CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(15350);
			cr_procfile.Save();
		}





		private static void CreateImportFolders()
		{
			logger.Debug("Creating shares...");
			ImportFolderRepository repNetShares = new ImportFolderRepository();

			ImportFolder sn = repNetShares.GetByImportLocation(@"M:\[ Anime 2011 ]");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderName = "Anime 2011";
				sn.ImportFolderLocation = @"M:\[ Anime 2011 ]";
				repNetShares.Save(sn);
			}

			sn = repNetShares.GetByImportLocation(@"M:\[ Anime - DVD and Bluray IN PROGRESS ]");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderName = "Anime - DVD and Bluray IN PROGRESS";
				sn.ImportFolderLocation = @"M:\[ Anime - DVD and Bluray IN PROGRESS ]";
				repNetShares.Save(sn);
			}

			sn = repNetShares.GetByImportLocation(@"M:\[ Anime - DVD and Bluray COMPLETE ]");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderName = "Anime - DVD and Bluray COMPLETE";
				sn.ImportFolderLocation = @"M:\[ Anime - DVD and Bluray COMPLETE ]";
				repNetShares.Save(sn);
			}

			sn = repNetShares.GetByImportLocation(@"M:\[ Anime ]");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderName = "Anime";
				sn.ImportFolderLocation = @"M:\[ Anime ]";
				repNetShares.Save(sn);
			}

			logger.Debug("Creating shares complete!");
		}

		private static void CreateImportFolders2()
		{
			logger.Debug("Creating shares...");
			ImportFolderRepository repNetShares = new ImportFolderRepository();

			ImportFolder sn = repNetShares.GetByImportLocation(@"F:\Anime1");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderName = "Anime1";
				sn.ImportFolderLocation = @"F:\Anime1";
				repNetShares.Save(sn);
			}

			sn = repNetShares.GetByImportLocation(@"H:\Anime2");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderName = "Anime2";
				sn.ImportFolderLocation = @"H:\Anime2";
				repNetShares.Save(sn);
			}

			sn = repNetShares.GetByImportLocation(@"G:\Anime3");
			if (sn == null)
			{
				sn = new ImportFolder();
				sn.ImportFolderType = (int)ImportFolderType.HDD;
				sn.ImportFolderName = "Anime3";
				sn.ImportFolderLocation = @"G:\Anime3";
				repNetShares.Save(sn);
			}

			logger.Debug("Creating shares complete!");
		}



		private static void CreateTestCommandRequests()
		{
			CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(5415, false, true);
			cr_anime.Save();

			/*
			cr_anime = new CommandRequest_GetAnimeHTTP(7382); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(6239); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(69); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(6751); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(3168); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(4196); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(634); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(2002); cr_anime.Save();


			
			cr_anime = new CommandRequest_GetAnimeHTTP(1); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(2); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(3); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(4); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(5); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(6); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(7); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(8); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(9); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(10); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(11); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(12); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(13); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(14); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(15); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(16); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(17); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(18); cr_anime.Save();
			cr_anime = new CommandRequest_GetAnimeHTTP(19); cr_anime.Save();*/
		}

		#endregion
	}
}
