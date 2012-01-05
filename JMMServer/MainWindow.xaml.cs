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
using System.Data.SQLite;
using System.Data.SqlClient;
using System.Data;
using JMMServer.MyAnime2Helper;
using JMMServer.ImageDownload;
using Microsoft.SqlServer.Management.Smo;

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
		private static bool doneFirstTrakTinfo = false;
		private static Logger logger = LogManager.GetCurrentClassLogger();
		private static DateTime lastTraktInfoUpdate = DateTime.Now;

		//private static Uri baseAddress = new Uri("http://localhost:8111/JMMServer");
		private static string baseAddressImageString = @"http://localhost:{0}/JMMServerImage";
		private static string baseAddressBinaryString = @"http://localhost:{0}/JMMServerBinary";
		//private static Uri baseAddressTCP = new Uri("net.tcp://localhost:8112/JMMServerTCP");
		//private static ServiceHost host = null;
		//private static ServiceHost hostTCP = null;
		private static ServiceHost hostImage = null;
		private static ServiceHost hostBinary = null;

		private static BackgroundWorker workerImport = new BackgroundWorker();
		private static BackgroundWorker workerScanFolder = new BackgroundWorker();
		private static BackgroundWorker workerRemoveMissing = new BackgroundWorker();
		private static BackgroundWorker workerDeleteImportFolder = new BackgroundWorker();
		private static BackgroundWorker workerTraktFriends = new BackgroundWorker();
		private static BackgroundWorker workerMyAnime2 = new BackgroundWorker();
		private static BackgroundWorker workerMediaInfo = new BackgroundWorker();

		private static BackgroundWorker workerSetupDB = new BackgroundWorker();

		private static System.Timers.Timer autoUpdateTimer = null;
		private static System.Timers.Timer autoUpdateTimerShort = null;
		private static List<FileSystemWatcher> watcherVids = null;

		BackgroundWorker downloadImagesWorker = new BackgroundWorker();

		public static Uri baseAddressBinary
		{
			get
			{
				return new Uri(string.Format(baseAddressBinaryString, ServerSettings.JMMServerPort));
			}
		}

		public static Uri baseAddressImage
		{
			get
			{
				return new Uri(string.Format(baseAddressImageString, ServerSettings.JMMServerPort));
			}
		}

		public MainWindow()
		{
			InitializeComponent();
			ServerSettings.DebugSettingsToLog();


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

			ServerState.Instance.DatabaseAvailable = false;
			ServerState.Instance.ServerOnline = false;
			ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();

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
			btnImportManualLinks.Click += new RoutedEventHandler(btnImportManualLinks_Click);
			btnUpdateAniDBInfo.Click += new RoutedEventHandler(btnUpdateAniDBInfo_Click);

			this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
			downloadImagesWorker.DoWork += new DoWorkEventHandler(downloadImagesWorker_DoWork);
			downloadImagesWorker.WorkerSupportsCancellation = true;

			txtServerPort.Text = ServerSettings.JMMServerPort;

			btnToolbarHelp.Click += new RoutedEventHandler(btnToolbarHelp_Click);
			btnApplyServerPort.Click += new RoutedEventHandler(btnApplyServerPort_Click);
			btnUpdateMediaInfo.Click += new RoutedEventHandler(btnUpdateMediaInfo_Click);

			workerMyAnime2.DoWork += new DoWorkEventHandler(workerMyAnime2_DoWork);
			workerMyAnime2.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerMyAnime2_RunWorkerCompleted);
			workerMyAnime2.ProgressChanged += new ProgressChangedEventHandler(workerMyAnime2_ProgressChanged);
			workerMyAnime2.WorkerReportsProgress = true;

			workerMediaInfo.DoWork += new DoWorkEventHandler(workerMediaInfo_DoWork);

			workerImport.WorkerReportsProgress = true;
			workerImport.WorkerSupportsCancellation = true;
			workerImport.DoWork += new DoWorkEventHandler(workerImport_DoWork);

			workerScanFolder.WorkerReportsProgress = true;
			workerScanFolder.WorkerSupportsCancellation = true;
			workerScanFolder.DoWork += new DoWorkEventHandler(workerScanFolder_DoWork);

			workerRemoveMissing.WorkerReportsProgress = true;
			workerRemoveMissing.WorkerSupportsCancellation = true;
			workerRemoveMissing.DoWork += new DoWorkEventHandler(workerRemoveMissing_DoWork);

			workerDeleteImportFolder.WorkerReportsProgress = false;
			workerDeleteImportFolder.WorkerSupportsCancellation = true;
			workerDeleteImportFolder.DoWork += new DoWorkEventHandler(workerDeleteImportFolder_DoWork);

			workerTraktFriends.DoWork += new DoWorkEventHandler(workerTraktFriends_DoWork);
			workerTraktFriends.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerTraktFriends_RunWorkerCompleted);

			workerSetupDB.DoWork += new DoWorkEventHandler(workerSetupDB_DoWork);
			workerSetupDB.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerSetupDB_RunWorkerCompleted);

			//StartUp();

			cboDatabaseType.Items.Clear();
			cboDatabaseType.Items.Add("SQLite");
			cboDatabaseType.Items.Add("Microsoft SQL Server 2008");
			cboDatabaseType.Items.Add("MySQL");
			cboDatabaseType.SelectionChanged += new System.Windows.Controls.SelectionChangedEventHandler(cboDatabaseType_SelectionChanged);

			cboImagesPath.Items.Clear();
			cboImagesPath.Items.Add("Default");
			cboImagesPath.Items.Add("Custom");
			cboImagesPath.SelectionChanged += new System.Windows.Controls.SelectionChangedEventHandler(cboImagesPath_SelectionChanged);
			btnChooseImagesFolder.Click += new RoutedEventHandler(btnChooseImagesFolder_Click);

			if (ServerSettings.BaseImagesPathIsDefault)
				cboImagesPath.SelectedIndex = 0;
			else
				cboImagesPath.SelectedIndex = 1;

			btnSaveDatabaseSettings.Click += new RoutedEventHandler(btnSaveDatabaseSettings_Click);
			btnRefreshMSSQLServerList.Click += new RoutedEventHandler(btnRefreshMSSQLServerList_Click);
			
		}

		

		

		void btnChooseImagesFolder_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				ServerSettings.BaseImagesPath = dialog.SelectedPath;
			}
		}

		void cboImagesPath_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (cboImagesPath.SelectedIndex == 0)
			{
				ServerSettings.BaseImagesPathIsDefault = true;
				btnChooseImagesFolder.Visibility = System.Windows.Visibility.Hidden;
			}
			else
			{
				ServerSettings.BaseImagesPathIsDefault = false;
				btnChooseImagesFolder.Visibility = System.Windows.Visibility.Visible;
			}
			
		}

		#region Database settings and initial start up

		void btnSaveDatabaseSettings_Click(object sender, RoutedEventArgs e)
		{

			try
			{
				btnSaveDatabaseSettings.IsEnabled = false;
				cboDatabaseType.IsEnabled = false;
				btnRefreshMSSQLServerList.IsEnabled = false;

				if (ServerState.Instance.DatabaseIsSQLite)
				{
					ServerSettings.DatabaseType = "SQLite";
				}
				else if (ServerState.Instance.DatabaseIsSQLServer)
				{
					if (string.IsNullOrEmpty(txtMSSQL_DatabaseName.Text) || string.IsNullOrEmpty(txtMSSQL_Password.Password)
						|| string.IsNullOrEmpty(cboMSSQLServerList.Text) || string.IsNullOrEmpty(txtMSSQL_Username.Text))
					{
						MessageBox.Show("Please fill out all the settings", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						txtMSSQL_DatabaseName.Focus();
						return;
					}

					ServerSettings.DatabaseType = "SQLServer";
					ServerSettings.DatabaseName = txtMSSQL_DatabaseName.Text;
					ServerSettings.DatabasePassword = txtMSSQL_Password.Password;
					ServerSettings.DatabaseServer = cboMSSQLServerList.Text;
					ServerSettings.DatabaseUsername = txtMSSQL_Username.Text;

				}
				else if (ServerState.Instance.DatabaseIsMySQL)
				{

					if (string.IsNullOrEmpty(txtMySQL_DatabaseName.Text) || string.IsNullOrEmpty(txtMySQL_Password.Password)
						|| string.IsNullOrEmpty(txtMySQL_ServerAddress.Text) || string.IsNullOrEmpty(txtMySQL_Username.Text))
					{
						MessageBox.Show("Please fill out all the settings", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						txtMySQL_DatabaseName.Focus();
						return;
					}

					ServerSettings.DatabaseType = "MySQL";
					ServerSettings.MySQL_SchemaName = txtMySQL_DatabaseName.Text;
					ServerSettings.MySQL_Password = txtMySQL_Password.Password;
					ServerSettings.MySQL_Hostname = txtMySQL_ServerAddress.Text;
					ServerSettings.MySQL_Username = txtMySQL_Username.Text;

				}

				workerSetupDB.RunWorkerAsync();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.Message, ex);
				MessageBox.Show("Failed to set start: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		void cboDatabaseType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			ServerState.Instance.DatabaseIsSQLite = false;
			ServerState.Instance.DatabaseIsSQLServer = false;
			ServerState.Instance.DatabaseIsMySQL = false;

			switch (cboDatabaseType.SelectedIndex)
			{
				case 0: 
					ServerState.Instance.DatabaseIsSQLite = true; 
					break;
				case 1:

					bool anySettingsMSSQL= !string.IsNullOrEmpty(ServerSettings.DatabaseName) || !string.IsNullOrEmpty(ServerSettings.DatabasePassword)
						|| !string.IsNullOrEmpty(ServerSettings.DatabaseServer) || !string.IsNullOrEmpty(ServerSettings.DatabaseUsername);

					if (anySettingsMSSQL)
					{
						txtMSSQL_DatabaseName.Text = ServerSettings.DatabaseName;
						txtMSSQL_Password.Password = ServerSettings.DatabasePassword;
						
						cboMSSQLServerList.Text = ServerSettings.DatabaseServer;
						txtMSSQL_Username.Text = ServerSettings.DatabaseUsername;
					}
					else
					{
						txtMSSQL_DatabaseName.Text = "JMMServer";
						txtMSSQL_Password.Password = "";
						cboMSSQLServerList.Text = "localhost";
						txtMSSQL_Username.Text = "sa";
					}
					ServerState.Instance.DatabaseIsSQLServer = true; 
					break;
				case 2: 

					bool anySettingsMySQL= !string.IsNullOrEmpty(ServerSettings.MySQL_SchemaName) || !string.IsNullOrEmpty(ServerSettings.MySQL_Password)
						|| !string.IsNullOrEmpty(ServerSettings.MySQL_Hostname) || !string.IsNullOrEmpty(ServerSettings.MySQL_Username);

					if (anySettingsMySQL)
					{
						txtMySQL_DatabaseName.Text = ServerSettings.MySQL_SchemaName;
						txtMySQL_Password.Password = ServerSettings.MySQL_Password;
						txtMySQL_ServerAddress.Text = ServerSettings.MySQL_Hostname;
						txtMySQL_Username.Text = ServerSettings.MySQL_Username;
					}
					else
					{
						txtMySQL_DatabaseName.Text = "JMMServer";
						txtMySQL_Password.Password = "";
						txtMySQL_ServerAddress.Text = "localhost";
						txtMySQL_Username.Text = "root";
					}

					ServerState.Instance.DatabaseIsMySQL = true; 
					break;
			}
		}

		void workerSetupDB_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			bool setupComplete = bool.Parse(e.Result.ToString());
			if (setupComplete)
			{
				ServerInfo.Instance.RefreshImportFolders();
				ServerState.Instance.CurrentSetupStatus = "Complete!";
				ServerState.Instance.ServerOnline = true;
				
				tabControl1.SelectedIndex = 0;
			}
			else
			{
				ServerState.Instance.ServerOnline = false;
				if (string.IsNullOrEmpty(ServerSettings.DatabaseType))
				{
					ServerSettings.DatabaseType = "SQLite";
					ShowDatabaseSetup();
				}
			}

			btnSaveDatabaseSettings.IsEnabled = true;
			cboDatabaseType.IsEnabled = true;
			btnRefreshMSSQLServerList.IsEnabled = true;
		}

		void btnRefreshMSSQLServerList_Click(object sender, RoutedEventArgs e)
		{
			btnSaveDatabaseSettings.IsEnabled = false;
			cboDatabaseType.IsEnabled = false;
			btnRefreshMSSQLServerList.IsEnabled = false;

			try
			{
				this.Cursor = Cursors.Wait;
				cboMSSQLServerList.Items.Clear();
				DataTable dt = SmoApplication.EnumAvailableSqlServers();
				foreach (DataRow dr in dt.Rows)
				{
					cboMSSQLServerList.Items.Add(dr[0]);
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			this.Cursor = Cursors.Arrow;
			btnSaveDatabaseSettings.IsEnabled = true;
			cboDatabaseType.IsEnabled = true;
			btnRefreshMSSQLServerList.IsEnabled = true;
		}

		private void ShowDatabaseSetup()
		{
			if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLITE") cboDatabaseType.SelectedIndex = 0;
			if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLSERVER") cboDatabaseType.SelectedIndex = 1;
			if (ServerSettings.DatabaseType.Trim().ToUpper() == "MYSQL") cboDatabaseType.SelectedIndex = 2;
		}

		void workerSetupDB_DoWork(object sender, DoWorkEventArgs e)
		{
			
			try
			{
				ServerState.Instance.ServerOnline = false;
				ServerState.Instance.CurrentSetupStatus = "Cleaning up...";

				StopWatchingFiles();
				AniDBDispose();
				StopHost();

				JMMService.CmdProcessorGeneral.Stop();
				JMMService.CmdProcessorHasher.Stop();
				JMMService.CmdProcessorImages.Stop();


				// wait until the queue count is 0
				// ie the cancel has actuall worked
				while (true)
				{
					if (JMMService.CmdProcessorGeneral.QueueCount == 0 && JMMService.CmdProcessorHasher.QueueCount == 0 && JMMService.CmdProcessorImages.QueueCount == 0) break;
					Thread.Sleep(250);
				}

				if (autoUpdateTimer != null) autoUpdateTimer.Enabled = false;
				if (autoUpdateTimerShort != null) autoUpdateTimerShort.Enabled = false;

				JMMService.CloseSessionFactory();

				ServerState.Instance.CurrentSetupStatus = "Initializing...";
				Thread.Sleep(1000);

				ServerState.Instance.CurrentSetupStatus = "Setting up database...";
				if (!DatabaseHelper.InitDB())
				{
					ServerState.Instance.DatabaseAvailable = false;

					if (string.IsNullOrEmpty(ServerSettings.DatabaseType))
						ServerState.Instance.CurrentSetupStatus = "Please select and configure your database.";
					else
						ServerState.Instance.CurrentSetupStatus = "Failed to start. Please review database settings.";
					e.Result = false;
					return;
				}
				else
					ServerState.Instance.DatabaseAvailable = true;


				//init session factory
				ServerState.Instance.CurrentSetupStatus = "Initializing Session Factory...";
				ISessionFactory temp = JMMService.SessionFactory;

				ServerState.Instance.CurrentSetupStatus = "Initializing Hosts...";
				SetupAniDBProcessor();
				StartImageHost();
				StartBinaryHost();

				//  Load all stats
				ServerState.Instance.CurrentSetupStatus = "Initializing Stats...";
				StatsCache.Instance.InitStats();

				ServerState.Instance.CurrentSetupStatus = "Initializing Queue Processors...";
				JMMService.CmdProcessorGeneral.Init();
				JMMService.CmdProcessorHasher.Init();
				JMMService.CmdProcessorImages.Init();

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

				ServerState.Instance.CurrentSetupStatus = "Initializing File Watchers...";
				StartWatchingFiles();

				DownloadAllImages();
				if (ServerSettings.RunImportOnStart) RunImport();


				ServerState.Instance.ServerOnline = true;
				e.Result = true;

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				ServerState.Instance.CurrentSetupStatus = ex.Message;
				e.Result = false;
			}
		}

		#endregion

		#region Update all media info
		void btnUpdateMediaInfo_Click(object sender, RoutedEventArgs e)
		{
			RefreshAllMediaInfo();
			MessageBox.Show("Actions have been queued", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void workerMediaInfo_DoWork(object sender, DoWorkEventArgs e)
		{
			VideoLocalRepository repVidLocals = new VideoLocalRepository();

			// first build a list of files that we already know about, as we don't want to process them again
			List<VideoLocal> filesAll = repVidLocals.GetAll();
			Dictionary<string, VideoLocal> dictFilesExisting = new Dictionary<string, VideoLocal>();
			foreach (VideoLocal vl in filesAll)
			{
				CommandRequest_ReadMediaInfo cr = new CommandRequest_ReadMediaInfo(vl.VideoLocalID);
				cr.Save();
			}
		}

		public static void RefreshAllMediaInfo()
		{
			if (workerMediaInfo.IsBusy) return;
			workerMediaInfo.RunWorkerAsync();
		}

		#endregion

		#region MyAnime2 Migration

		void workerMyAnime2_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			MA2Progress ma2Progress = e.UserState as MA2Progress;
			if (!string.IsNullOrEmpty(ma2Progress.ErrorMessage))
			{
				txtMA2Progress.Text = ma2Progress.ErrorMessage;
				txtMA2Success.Visibility = System.Windows.Visibility.Hidden;
				return;
			}

			if (ma2Progress.CurrentFile <= ma2Progress.TotalFiles)
				txtMA2Progress.Text = string.Format("Processing unlinked file {0} of {1}", ma2Progress.CurrentFile, ma2Progress.TotalFiles);
			else
				txtMA2Progress.Text = string.Format("Processed all unlinked files ({0})", ma2Progress.TotalFiles);
			txtMA2Success.Text = string.Format("{0} files sucessfully migrated", ma2Progress.MigratedFiles);
		}

		void workerMyAnime2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			
		}

		void workerMyAnime2_DoWork(object sender, DoWorkEventArgs e)
		{
			MA2Progress ma2Progress = new MA2Progress();
			ma2Progress.CurrentFile = 0;
			ma2Progress.ErrorMessage = "";
			ma2Progress.MigratedFiles = 0;
			ma2Progress.TotalFiles = 0;

			try
			{
				string databasePath = e.Argument as string;

				string connString = string.Format(@"data source={0};useutf16encoding=True", databasePath);
				SQLiteConnection myConn = new SQLiteConnection(connString);
				myConn.Open();

				// get a list of unlinked files
				VideoLocalRepository repVids = new VideoLocalRepository();
				AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
				AniDB_AnimeRepository repAniAnime = new AniDB_AnimeRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();

				List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
				ma2Progress.TotalFiles = vids.Count;
				
				foreach (VideoLocal vid in vids)
				{
					ma2Progress.CurrentFile = ma2Progress.CurrentFile + 1;
					workerMyAnime2.ReportProgress(0, ma2Progress);

					// search for this file in the XrossRef table in MA2
					string sql = string.Format("SELECT AniDB_EpisodeID from CrossRef_Episode_FileHash WHERE Hash = '{0}' AND FileSize = {1}", vid.ED2KHash, vid.FileSize);
					SQLiteCommand sqCommand = new SQLiteCommand(sql);
					sqCommand.Connection = myConn;

					SQLiteDataReader myReader = sqCommand.ExecuteReader();
					while (myReader.Read())
					{
						int episodeID = myReader.GetInt32(0);
						if (episodeID <= 0) continue;

						sql = string.Format("SELECT AnimeID from AniDB_Episode WHERE EpisodeID = {0}", episodeID);
						sqCommand = new SQLiteCommand(sql);
						sqCommand.Connection = myConn;

						SQLiteDataReader myReader2 = sqCommand.ExecuteReader();
						while (myReader2.Read())
						{
							int animeID = myReader2.GetInt32(0);

							// so now we have all the needed details we can link the file to the episode
							// as long as wehave the details in JMM
							AniDB_Anime anime = null;
							AniDB_Episode ep = repAniEps.GetByEpisodeID(episodeID);
							if (ep == null)
							{
								logger.Debug("Getting Anime record from AniDB....");
								anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true, ServerSettings.AniDB_DownloadRelatedAnime);
							}
							else
								anime = repAniAnime.GetByAnimeID(animeID);

							// create the group/series/episode records if needed
							AnimeSeries ser = null;
							if (anime == null) continue;

							logger.Debug("Creating groups, series and episodes....");
							// check if there is an AnimeSeries Record associated with this AnimeID
							ser = repSeries.GetByAnimeID(animeID);
							if (ser == null)
							{
								// create a new AnimeSeries record
								ser = anime.CreateAnimeSeriesAndGroup();
							}


							ser.CreateAnimeEpisodes();

							// check if we have any group status data for this associated anime
							// if not we will download it now
							AniDB_GroupStatusRepository repStatus = new AniDB_GroupStatusRepository();
							if (repStatus.GetByAnimeID(anime.AnimeID).Count == 0)
							{
								CommandRequest_GetReleaseGroupStatus cmdStatus = new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
								cmdStatus.Save();
							}

							// update stats
							ser.EpisodeAddedDate = DateTime.Now;
							repSeries.Save(ser);

							AnimeGroupRepository repGroups = new AnimeGroupRepository();
							foreach (AnimeGroup grp in ser.AllGroupsAbove)
							{
								grp.EpisodeAddedDate = DateTime.Now;
								repGroups.Save(grp);
							}
							

							AnimeEpisode epAnime = repEps.GetByAniDBEpisodeID(episodeID);
							if (epAnime == null)
								continue;

							CrossRef_File_EpisodeRepository repXRefs = new CrossRef_File_EpisodeRepository();
							CrossRef_File_Episode xref = new CrossRef_File_Episode();

							try
							{
								xref.PopulateManually(vid, epAnime);
							}
							catch (Exception ex)
							{
								string msg = string.Format("Error populating XREF: {0} - {1}", vid.ToStringDetailed(), ex.ToString());
								throw;
							}

							repXRefs.Save(xref);

							vid.MoveFileIfRequired();

							// update stats for groups and series
							if (ser != null)
							{
								// update all the groups above this series in the heirarchy
								ser.UpdateStats(true, true, true);
								StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
							}


							// Add this file to the users list
							if (ServerSettings.AniDB_MyList_AddFiles)
							{
								CommandRequest_AddFileToMyList cmd = new CommandRequest_AddFileToMyList(vid.ED2KHash);
								cmd.Save();
							}

							ma2Progress.MigratedFiles = ma2Progress.MigratedFiles + 1;
							workerMyAnime2.ReportProgress(0, ma2Progress);

						}
						myReader2.Close();
						

						//Console.WriteLine(myReader.GetString(0));
					}
					myReader.Close();
				}


				myConn.Close();

				ma2Progress.CurrentFile = ma2Progress.CurrentFile + 1;
				workerMyAnime2.ReportProgress(0, ma2Progress);

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				ma2Progress.ErrorMessage = ex.Message;
				workerMyAnime2.ReportProgress(0, ma2Progress);
			}
		}

		void btnImportManualLinks_Click(object sender, RoutedEventArgs e)
		{
			if (workerMyAnime2.IsBusy)
			{
				MessageBox.Show("Importer is already running", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			txtMA2Progress.Visibility = System.Windows.Visibility.Visible;
			txtMA2Success.Visibility = System.Windows.Visibility.Visible;

			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.Filter = "Sqlite Files (*.DB3)|*.db3";
			ofd.ShowDialog();
			if (!string.IsNullOrEmpty(ofd.FileName))
			{
				workerMyAnime2.RunWorkerAsync(ofd.FileName);
			}
		}

		private void ImportLinksFromMA2(string databasePath)
		{
			
		}

		#endregion

		void btnApplyServerPort_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(txtServerPort.Text))
			{
				MessageBox.Show("Please enter a value", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				txtServerPort.Focus();
				return;
			}

			int port = 0;
			int.TryParse(txtServerPort.Text, out port);
			if (port <= 0 || port > 65535)
			{
				MessageBox.Show("Please enter a value between 1 and 65535", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				txtServerPort.Focus();
				return;
			}

			try
			{
				ServerSettings.JMMServerPort = port.ToString();

				this.Cursor = Cursors.Wait;

				JMMService.CmdProcessorGeneral.Paused = true;
				JMMService.CmdProcessorHasher.Paused = true;
				JMMService.CmdProcessorImages.Paused = true;

				StopHost();
				StartBinaryHost();
				StartImageHost();

				JMMService.CmdProcessorGeneral.Paused = false;
				JMMService.CmdProcessorHasher.Paused = false;
				JMMService.CmdProcessorImages.Paused = false;

				this.Cursor = Cursors.Arrow;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		void btnToolbarHelp_Click(object sender, RoutedEventArgs e)
		{
			//AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			//AnimeSeries ser = repSeries.GetByID(222);
			//ser.UpdateStats(true, true, true);

			//TraktTVHelper.GetFriendsRequests();

			//FileHashHelper.GetMediaInfo(@"C:\[Hiryuu] Maken-Ki! 09 [Hi10P 1280x720 H264] [EE47C947].mkv", true);

			//CommandRequest_ReadMediaInfo cr1 = new CommandRequest_ReadMediaInfo(2038);
			//cr1.Save();

			//CommandRequest_ReadMediaInfo cr2 = new CommandRequest_ReadMediaInfo(2037);
			//cr2.Save();

			/*AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			foreach (AniDB_Anime anime in repAnime.GetAll())
			{
				List<TraktTV_ShoutGet> shouts = TraktTVHelper.GetShowShouts(anime.AnimeID);
				if (shouts == null || shouts.Count == 0)
				{
					//logger.Info("{0} ({1}) = 0 Shouts", anime.MainTitle, anime.AnimeID);
				}
				else
				{
					if (shouts.Count <= 5)
						logger.Info("{0} ({1}) = {2} MINOR Shouts", anime.MainTitle, anime.AnimeID, shouts.Count);
					else
						logger.Info("{0} ({1}) = {2} *** MAJOR *** Shouts", anime.MainTitle, anime.AnimeID, shouts.Count);
				}
			}*/


			
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
			//ServerInfo.Instance.RefreshImportFolders();

			tabControl1.SelectedIndex = 4; // setup

			ShowDatabaseSetup();

			workerSetupDB.RunWorkerAsync();
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

		void btnUpdateAniDBInfo_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this.Cursor = Cursors.Wait;
				Importer.RunImport_UpdateAllAniDB();
				this.Cursor = Cursors.Arrow;
				MessageBox.Show("Updates are queued", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.Message, ex);
			}
		}

		void btnUpdateTvDBInfo_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this.Cursor = Cursors.Wait;
				Importer.RunImport_UpdateTvDB(false);
				this.Cursor = Cursors.Arrow;
				MessageBox.Show("Updates are queued", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.Message, ex);
			}
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
			this.Cursor = Cursors.Wait;
			TraktTVHelper.SyncCollectionToTrakt();
			this.Cursor = Cursors.Arrow;
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
			
		}

		void workerTraktFriends_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			//StatsCache.Instance.TraktFriendRequests.Clear();
			//StatsCache.Instance.TraktFriendActivityInfo.Clear();

			List<object> allInfo = e.Result as List<object>;
			if (allInfo != null && allInfo.Count > 0)
			{
				foreach (object obj in allInfo)
				{
					if (obj.GetType() == typeof(TraktTVFriendRequest))
					{
						TraktTVFriendRequest req = obj as TraktTVFriendRequest;
						StatsCache.Instance.TraktFriendRequests.Add(req);
					}

					if (obj.GetType() == typeof(TraktTV_Activity))
					{
						TraktTV_Activity act = obj as TraktTV_Activity;
						StatsCache.Instance.TraktFriendActivityInfo.Add(act);
					}
				}
			}
		}

		void workerTraktFriends_DoWork(object sender, DoWorkEventArgs e)
		{
			List<object> allInfo = new List<object>();

			if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
			{
				e.Result = allInfo;
				return;
			}

			List<TraktTVFriendRequest> requests = TraktTVHelper.GetFriendsRequests();
			if (requests != null)
			{
				foreach (TraktTVFriendRequest req in requests)
					allInfo.Add(req);
			}

			TraktTV_ActivitySummary summ = TraktTVHelper.GetActivityFriends();
			if (summ != null)
			{
				foreach (TraktTV_Activity act in summ.activity)
					allInfo.Add(act);
			}

			e.Result = allInfo;
		}

		public static void UpdateTraktFriendInfo(bool forced)
		{
			if (workerTraktFriends.IsBusy) return;

			if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password)) return;

			bool performUpdate = false;
			if (!doneFirstTrakTinfo || forced)
				performUpdate = true;
			else
			{
				TimeSpan ts = DateTime.Now - lastTraktInfoUpdate;
				if (ts.TotalMinutes > 20) performUpdate = true;
			}

			if (performUpdate)
			{
				StatsCache.Instance.TraktFriendRequests.Clear();
				StatsCache.Instance.TraktFriendActivityInfo.Clear();

				lastTraktInfoUpdate = DateTime.Now;
				doneFirstTrakTinfo = true;
				workerTraktFriends.RunWorkerAsync();
			}
		}
		

		void autoUpdateTimerShort_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			JMMService.CmdProcessorImages.NotifyOfNewCommand();

			UpdateTraktFriendInfo(false);
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
			StopWatchingFiles();

			watcherVids = new List<FileSystemWatcher>();

			ImportFolderRepository repNetShares = new ImportFolderRepository();
			foreach (ImportFolder share in repNetShares.GetAll())
			{
				try
				{
					if (Directory.Exists(share.ImportFolderLocation) && share.FolderIsWatched)
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
			if (e.ChangeType == WatcherChangeTypes.Created && FileHashHelper.IsVideo(e.FullPath))
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

		public static void DeleteImportFolder(int importFolderID)
		{
			if (!workerDeleteImportFolder.IsBusy)
				workerDeleteImportFolder.RunWorkerAsync(importFolderID);
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

		void workerDeleteImportFolder_DoWork(object sender, DoWorkEventArgs e)
		{
			try
			{
				int importFolderID = int.Parse(e.Argument.ToString());
				Importer.DeleteImportFolder(importFolderID);
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
				logger.ErrorException(ex.ToString(), ex);
			}
		}

		/*private static void StartHost()
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
		}*/

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
