using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AniDBAPI;
using FluentNHibernate.MappingModel;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Commands;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Modules
{
    public class Core : NancyModule
    {
        private Logger logger = LogManager.GetCurrentClassLogger();
        public Core() : base("/api")
        {
            // As this module requireAuthentication all request need to have apikey in header.

            this.RequiresAuthentication();

            #region 01.Settings

            Post["/config/port/set", true] = async (x,ct) => await Task.Factory.StartNew(SetPort, ct);
            Get["/config/port/get", true] = async (x,ct) => await Task.Factory.StartNew(GetPort, ct);
            Post["/config/imagepath/set", true] = async (x,ct) => await Task.Factory.StartNew(SetImagepath, ct);
            Get["/config/imagepath/get", true] = async (x,ct) => await Task.Factory.StartNew(GetImagepath, ct);
            Get["/config/export", true] = async (x,ct) => await Task.Factory.StartNew(ExportConfig, ct);
            Post["/config/import", true] = async (x,ct) => await Task.Factory.StartNew(ImportConfig, ct);
            Post["/config/set", true] = async (x, ct) => await Task.Factory.StartNew(SetSetting, ct);
            Post["/config/get", true] = async (x, ct) => await Task.Factory.StartNew(GetSetting, ct);

            #endregion

            #region 02.AniDB

            Post["/anidb/set", true] = async (x,ct) => await Task.Factory.StartNew(SetAniDB, ct);
            Get["/anidb/get", true] = async (x,ct) => await Task.Factory.StartNew(GetAniDB, ct);
            Get["/anidb/test", true] = async (x,ct) => await Task.Factory.StartNew(TestAniDB, ct);
            Get["/anidb/votes/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncAniDBVotes, ct);
            Get["/anidb/list/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncAniDBList, ct);
            Get["/anidb/update", true] = async (x,ct) => await Task.Factory.StartNew(UpdateAllAniDB, ct);
            Get["/anidb/updatemissingcache", true] = async (x,ct) => await Task.Factory.StartNew(UpdateMissingAniDBXML, ct);

            #endregion

            #region 03.MyAnimeList

            Post["/mal/set", true] = async (x,ct) => await Task.Factory.StartNew(SetMAL, ct);
            Get["/mal/get", true] = async (x,ct) => await Task.Factory.StartNew(GetMAL, ct);
            Get["/mal/test", true] = async (x,ct) => await Task.Factory.StartNew(TestMAL, ct);
            Get["/mal/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanMAL, ct);
            Get["/mal/download", true] = async (x,ct) => await Task.Factory.StartNew(DownloadFromMAL, ct);
            Get["/mal/upload", true] = async (x,ct) => await Task.Factory.StartNew(UploadToMAL, ct);
            //Get["/mal/votes/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncMALVotes, ct); <-- not implemented as CommandRequest

            #endregion

            #region 04.Trakt

            Post["/trakt/set"] = x => APIStatus.NotImplemented();
            Get["/trakt/get", true] = async (x,ct) => await Task.Factory.StartNew(GetTrakt, ct);
            Get["/trakt/create"] = x => APIStatus.NotImplemented();
            Get["/trakt/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncTrakt, ct);
            Get["/trakt/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanTrakt, ct);
            Get["/trakt/code", true] = async (x,ct) => await Task.Factory.StartNew(GetTraktCode, ct);

            #endregion

            #region 05.TvDB

            Get["/tvdb/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanTvDB, ct);
            Get["/tvdb/regenlinks", true] = async (x,ct) => await Task.Factory.StartNew(RegenerateAllEpisodeLinks, ct);
            Get["/tvdb/checklinks", true] = async (x,ct) => await Task.Factory.StartNew(CheckAllEpisodeLinksAgainstCurrent, ct);

            #endregion

            #region 06.MovieDB

            Get["/moviedb/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanMovieDB, ct);

            #endregion

            #region 07.User

            Get["/user/list", true] = async (x,ct) => await Task.Factory.StartNew(GetUsers, ct);
            Post["/user/create", true] = async (x,ct) => await Task.Factory.StartNew(CreateUser, ct);
            Post["/user/delete", true] = async (x,ct) => await Task.Factory.StartNew(DeleteUser, ct);
            Post["/user/password", true] = async (x,ct) => await Task.Factory.StartNew(ChangePassword, ct);
            Post["/user/password/{uid}", true] = async (x,ct) => await Task.Factory.StartNew(() => ChangePassword(x.uid), ct);

            #endregion

            #region 08.OS-based operations

            Get["/os/folder/base", true] = async (x,ct) => await Task.Factory.StartNew(GetOSBaseFolder, ct);
            Post["/os/folder", true] = async (x,ct) => await Task.Factory.StartNew(() => GetOSFolder(x.folder), ct);
            Get["/os/drives", true] = async (x,ct) => await Task.Factory.StartNew(GetOSDrives, ct);

            #endregion

            #region 09.Cloud accounts

            Get["/cloud/list", true] = async (x,ct) => await Task.Factory.StartNew(GetCloudAccounts, ct);
            Get["/cloud/count", true] = async (x,ct) => await Task.Factory.StartNew(GetCloudAccountsCount, ct);
            Post["/cloud/add", true] = async (x,ct) => await Task.Factory.StartNew(AddCloudAccount, ct);
            Post["/cloud/delete", true] = async (x,ct) => await Task.Factory.StartNew(DeleteCloudAccount, ct);
            Get["/cloud/import", true] = async (x,ct) => await Task.Factory.StartNew(RunCloudImport, ct);

            #endregion

            #region 10.Logs

            Get["/log/get", true] = async (x,ct) => await Task.Factory.StartNew(() => GetLog(10, 0), ct);
            Get["/log/get/{max}/{position}", true] = async (x,ct) => await Task.Factory.StartNew(() => GetLog((int) x.max, (int) x.position), ct);
            Post["/log/rotate", true] = async (x,ct) => await Task.Factory.StartNew(SetRotateLogs, ct);
            Get["/log/rotate", true] = async (x,ct) => await Task.Factory.StartNew(GetRotateLogs, ct);
            Get["/log/rotate/start", true] = async (x,ct) => await Task.Factory.StartNew(StartRotateLogs, ct);

            #endregion

            #region 11. Image Actions
            Get["/images/update", true] = async (x, ct) => await Task.Factory.StartNew(() => UpdateImages());
            #endregion
        }

        #region 01.Settings

        /// <summary>
        /// Set JMMServer Port
        /// </summary>
        /// <returns></returns>
        private object SetPort()
        {
            Credentials cred = this.Bind();
            if (cred.port != 0)
            {
                ServerSettings.JMMServerPort = cred.port.ToString();
                return APIStatus.OK();
            }
            return new APIMessage(400, "Port Missing");
        }

        /// <summary>
        /// Get JMMServer Port
        /// </summary>
        /// <returns></returns>
        private object GetPort()
        {
            dynamic x = new ExpandoObject();
            x.port = int.Parse(ServerSettings.JMMServerPort);
            return x;
        }

        /// <summary>
        /// Set Imagepath as default or custom
        /// </summary>
        /// <returns></returns>
        private object SetImagepath()
        {
            ImagePath imagepath = this.Bind();
            if (imagepath.isdefault)
            {
                ServerSettings.ImagesPath = ServerSettings.DefaultImagePath;
                return APIStatus.OK();
            }
            if (!String.IsNullOrEmpty(imagepath.path) && imagepath.path != string.Empty)
            {
                if (Directory.Exists(imagepath.path))
                {
                    ServerSettings.ImagesPath = imagepath.path;
                    return APIStatus.OK();
                }
                return new APIMessage(404, "Directory Not Found on Host");
            }
            return new APIMessage(400, "Path Missing");
        }

        /// <summary>
        /// Return ImagePath object
        /// </summary>
        /// <returns></returns>
        private object GetImagepath()
        {
            ImagePath imagepath = new ImagePath
            {
                path = ServerSettings.ImagesPath,
                isdefault = ServerSettings.ImagesPath == ServerSettings.DefaultImagePath
            };
            return imagepath;
        }

        /// <summary>
        /// Return body of current working settings.json - this could act as backup
        /// </summary>
        /// <returns></returns>
        private object ExportConfig()
        {
            try
            {
                return ServerSettings.appSettings;
            }
            catch
            {
                return APIStatus.InternalError("Error while reading settings.");
            }
        }

        /// <summary>
        /// Import config file that was sent to in API body - this act as import from backup
        /// </summary>
        /// <returns>APIStatus</returns>
        private object ImportConfig()
        {
            CL_ServerSettings settings = this.Bind();
            string raw_settings = settings.ToJSON();

            if (raw_settings.Length != new CL_ServerSettings().ToJSON().Length)
            {
                string path = Path.Combine(ServerSettings.ApplicationPath, "temp.json");
                File.WriteAllText(path, raw_settings, Encoding.UTF8);
                try
                {
                    ServerSettings.LoadSettingsFromFile(path, true);
                    return APIStatus.OK();
                }
                catch
                {
                    return APIStatus.InternalError("Error while importing settings");
                }
            }
            return APIStatus.BadRequest("Empty settings are not allowed");
        }

        /// <summary>
        /// Return given setting
        /// </summary>
        /// <returns></returns>
        private object GetSetting()
        {
            try
            {
                // TODO Refactor Settings to a POCO that is serialized, and at runtime, build a dictionary of types to validate against
                Setting setting = this.Bind();
                if (string.IsNullOrEmpty(setting?.setting)) return APIStatus.BadRequest("An invalid setting was passed");
                var value = typeof(ServerSettings).GetProperty(setting.setting)?.GetValue(null, null);
                if (value == null) return APIStatus.BadRequest("An invalid setting was passed");

                Setting returnSetting = new Setting
                {
                    setting = setting.setting,
                    value = value.ToString()
                };
                return returnSetting;
            }
            catch
            {
                return APIStatus.InternalError();
            }
        }

        /// <summary>
        /// Set given setting
        /// </summary>
        /// <returns></returns>
        private object SetSetting()
        {
            // TODO Refactor Settings to a POCO that is serialized, and at runtime, build a dictionary of types to validate against
            try
            {
                List<Setting> settings;

                try
                {
                    settings = this.Bind<List<Setting>>();
                }
                catch (ModelBindingException)
                {
                    settings = new List<Setting> { this.Bind<Setting>() };
                }

                List<APIMessage> errors = new List<APIMessage>();
                for (var index = 0; index < settings.Count; index++)
                {
                    var setting = settings[index];
                    if (string.IsNullOrEmpty(setting.setting))
                    {
                        errors.Add(APIStatus.BadRequest($"{index}: An invalid setting was passed"));
                        continue;
                    }

                    if (setting.value == null)
                    {
                        errors.Add(APIStatus.BadRequest($"{index}: An invalid value was passed"));
                        continue;
                    }

                    var property = typeof(ServerSettings).GetProperty(setting.setting);
                    if (property == null)
                    {
                        errors.Add(APIStatus.BadRequest($"{index}: An invalid setting was passed"));
                        continue;
                    }

                    if (!property.CanWrite)
                    {
                        errors.Add(APIStatus.BadRequest($"{index}: An invalid setting was passed"));
                        continue;
                    }
                    var settingType = property.PropertyType;
                    try
                    {
                        var converter = TypeDescriptor.GetConverter(settingType);
                        if (!converter.CanConvertFrom(typeof(string)))
                        {
                            errors.Add(APIStatus.BadRequest($"{index}: An invalid value was passed"));
                            continue;
                        }
                        var value = converter.ConvertFromInvariantString(setting.value);
                        if (value == null)
                        {
                            errors.Add(APIStatus.BadRequest($"{index}: An invalid value was passed"));
                            continue;
                        }
                        property.SetValue(null, value);
                    }
                    catch
                    {
                        errors.Add(APIStatus.BadRequest($"{index}: An invalid value was passed"));
                    }
                }

                if (errors.Count > 0)
                {
                    Context.Response.StatusCode = HttpStatusCode.BadRequest;
                    return errors;
                }

                return APIStatus.OK();
            }
            catch
            {
                return APIStatus.InternalError();
            }
        }

        #endregion

        #region 02.AniDB

        /// <summary>
        /// Set AniDB account with login, password and client port
        /// </summary>
        /// <returns></returns>
        private object SetAniDB()
        {
            Credentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.login) && cred.login != string.Empty && !String.IsNullOrEmpty(cred.password) &&
                cred.password != string.Empty)
            {
                ServerSettings.AniDB_Username = cred.login;
                ServerSettings.AniDB_Password = cred.password;
                if (cred.port != 0)
                {
                    ServerSettings.AniDB_ClientPort = cred.port.ToString();
                }
                return APIStatus.OK();
            }

            return new APIMessage(400, "Login and Password missing");
        }

        /// <summary>
        /// Test AniDB Creditentials
        /// </summary>
        /// <returns></returns>
        private object TestAniDB()
        {
            ShokoService.AnidbProcessor.ForceLogout();
            ShokoService.AnidbProcessor.CloseConnections();

            Thread.Sleep(1000);

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                ServerSettings.AniDB_ServerAddress,
                ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

            if (ShokoService.AnidbProcessor.Login())
            {
                ShokoService.AnidbProcessor.ForceLogout();
                return APIStatus.OK();
            }

            return APIStatus.Unauthorized();
        }

        /// <summary>
        /// Return login/password/port of used AniDB
        /// </summary>
        /// <returns></returns>
        private object GetAniDB()
        {
            Credentials cred = new Credentials
            {
                login = ServerSettings.AniDB_Username,
                password = ServerSettings.AniDB_Password,
                port = int.Parse(ServerSettings.AniDB_ClientPort)
            };
            return cred;
        }

        /// <summary>
        /// Sync votes bettween Local and AniDB and only upload to MAL
        /// </summary>
        /// <returns></returns>
        private object SyncAniDBVotes()
        {
            //TODO APIv2: Command should be split into AniDb/MAL sepereate
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
            return APIStatus.OK();
        }

        /// <summary>
        /// Sync AniDB List
        /// </summary>
        /// <returns></returns>
        private object SyncAniDBList()
        {
            ShokoServer.SyncMyList();
            return APIStatus.OK();
        }

        /// <summary>
        /// Update all series infromation from AniDB
        /// </summary>
        /// <returns></returns>
        private object UpdateAllAniDB()
        {
            Importer.RunImport_UpdateAllAniDB();
            return APIStatus.OK();
        }

        private object UpdateMissingAniDBXML()
        {
            try
            {
                var allAnime = RepoFactory.AniDB_Anime.GetAll().Select(a => a.AnimeID).OrderBy(a => a).ToList();
                logger.Info($"Starting the check for {allAnime.Count} anime XML files");
                int updatedAnime = 0;
                for (var i = 0; i < allAnime.Count; i++)
                {
                    var animeID = allAnime[i];
                    if (i % 10 == 1) logger.Info($"Checking anime {i + 1}/{allAnime.Count} for XML file");

                    var xml = APIUtils.LoadAnimeHTTPFromFile(animeID);
                    if (xml == null)
                    {
                        CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(animeID, true, false);
                        cmd.Save();
                        updatedAnime++;
                        continue;
                    }

                    var rawAnime = AniDBHTTPHelper.ProcessAnimeDetails(xml, animeID);
                    if (rawAnime == null)
                    {
                        CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(animeID, true, false);
                        cmd.Save();
                        updatedAnime++;
                    }
                }
                logger.Info($"Updating {updatedAnime} anime");
            }
            catch (Exception e)
            {
                logger.Error($"Error checking and queuing AniDB XML Updates: {e}");
                return APIStatus.InternalError(e.Message);
            }
            return APIStatus.OK();
        }

        #endregion

        #region 03.MyAnimeList

        /// <summary>
        /// Set MAL account with login, password
        /// </summary>
        /// <returns></returns>
        private object SetMAL()
        {
            Credentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.login) && cred.login != string.Empty && !String.IsNullOrEmpty(cred.password) &&
                cred.password != string.Empty)
            {
                ServerSettings.MAL_Username = cred.login;
                ServerSettings.MAL_Password = cred.password;
                return APIStatus.OK();
            }

            return new APIMessage(400, "Login and Password missing");
        }

        /// <summary>
        /// Return current used MAL Creditentials
        /// </summary>
        /// <returns></returns>
        private object GetMAL()
        {
            Credentials cred = new Credentials
            {
                login = ServerSettings.MAL_Username,
                password = ServerSettings.MAL_Password
            };
            return cred;
        }

        /// <summary>
        /// Test MAL Creditionals against MAL
        /// </summary>
        /// <returns></returns>
        private object TestMAL()
        {
            return MALHelper.VerifyCredentials()
                ? APIStatus.OK()
                : APIStatus.Unauthorized();
        }

        /// <summary>
        /// Scan MAL
        /// </summary>
        /// <returns></returns>
        private object ScanMAL()
        {
            Importer.RunImport_ScanMAL();
            return APIStatus.OK();
        }

        /// <summary>
        /// Download Watched States from MAL
        /// </summary>
        /// <returns></returns>
        private object DownloadFromMAL()
        {
            CommandRequest_MALDownloadStatusFromMAL cmd = new CommandRequest_MALDownloadStatusFromMAL();
            cmd.Save();
            return APIStatus.OK();
        }

        /// <summary>
        /// Upload Watched States to MAL
        /// </summary>
        /// <returns></returns>
        private object UploadToMAL()
        {
            CommandRequest_MALUploadStatusToMAL cmd = new CommandRequest_MALUploadStatusToMAL();
            cmd.Save();
            return APIStatus.OK();
        }

        #endregion

        #region 04.Trakt

        /// <summary>
        /// Get Trakt code and url
        /// </summary>
        /// <returns></returns>
        private object GetTraktCode()
        {
            var code = new ShokoServiceImplementation().GetTraktDeviceCode();
            if (code.UserCode == string.Empty)
                return APIStatus.InternalError();

            Dictionary<string, object> result = new Dictionary<string, object>();
            result.Add("usercode", code.UserCode);
            result.Add("url", code.VerificationUrl);
            return result;
        }

        /// <summary>
        /// Return trakt authtoken
        /// </summary>
        /// <returns></returns>
        private object GetTrakt()
        {
            Credentials cred = new Credentials
            {
                token = ServerSettings.Trakt_AuthToken,
                refresh_token = ServerSettings.Trakt_RefreshToken
            };
            return cred;
        }

        /// <summary>
        /// Sync Trakt Collection
        /// </summary>
        /// <returns></returns>
        private object SyncTrakt()
        {
            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(true);
                cmd.Save();
                return APIStatus.OK();
            }

            return new APIMessage(204, "Trakt is not enabled or you are missing the authtoken");
        }

        /// <summary>
        /// Scan Trakt
        /// </summary>
        /// <returns></returns>
        private object ScanTrakt()
        {
            Importer.RunImport_ScanTrakt();
            return APIStatus.OK();
        }

        #endregion

        #region 05.TvDB

        /// <summary>
        /// Scan TvDB
        /// </summary>
        /// <returns></returns>
        private object ScanTvDB()
        {
            Importer.RunImport_ScanTvDB();
            return APIStatus.OK();
        }

        private object RegenerateAllEpisodeLinks()
        {
            try
            {
                RepoFactory.CrossRef_AniDB_TvDB_Episode.DeleteAllUnverifiedLinks();
                RepoFactory.AnimeSeries.GetAll().ToList().AsParallel().ForAll(animeseries =>
                    TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeseries.AniDB_ID, true));
            }
            catch (Exception e)
            {
                logger.Error(e);
                return APIStatus.InternalError(e.Message);
            }

            return APIStatus.OK();
        }

        private class EpisodeMatchComparison
        {
            public string Anime { get; set; }
            public int AnimeID { get; set; }
            public IEnumerable<(AniEpSummary AniDB, TvDBEpSummary TvDB)> Current { get; set; }
            public IEnumerable<(AniEpSummary AniDB, TvDBEpSummary TvDB)> Calculated { get; set; }
        }

        private class AniEpSummary
        {
            public int AniDBEpisodeType { get; set; }
            public int AniDBEpisodeNumber { get; set; }
            public string AniDBEpisodeName { get; set; }

            protected bool Equals(AniEpSummary other)
            {
                return AniDBEpisodeType == other.AniDBEpisodeType && AniDBEpisodeNumber == other.AniDBEpisodeNumber && string.Equals(AniDBEpisodeName, other.AniDBEpisodeName);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((AniEpSummary) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = AniDBEpisodeType;
                    hashCode = (hashCode * 397) ^ AniDBEpisodeNumber;
                    hashCode = (hashCode * 397) ^ (AniDBEpisodeName != null ? AniDBEpisodeName.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private class TvDBEpSummary
        {
            public int TvDBSeason { get; set; }
            public int TvDBEpisodeNumber { get; set; }
            public string TvDBEpisodeName { get; set; }

            protected bool Equals(TvDBEpSummary other)
            {
                return TvDBSeason == other.TvDBSeason && TvDBEpisodeNumber == other.TvDBEpisodeNumber && string.Equals(TvDBEpisodeName, other.TvDBEpisodeName);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TvDBEpSummary) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TvDBSeason;
                    hashCode = (hashCode * 397) ^ TvDBEpisodeNumber;
                    hashCode = (hashCode * 397) ^ (TvDBEpisodeName != null ? TvDBEpisodeName.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private object CheckAllEpisodeLinksAgainstCurrent()
        {
            try
            {
                // This is for testing changes in the algorithm. It will be slow.
                var list = RepoFactory.AnimeSeries.GetAll().Select(a => a.GetAnime())
                    .Where(a => !string.IsNullOrEmpty(a?.MainTitle)).OrderBy(a => a.MainTitle).ToList();
                var result = new List<EpisodeMatchComparison>();
                foreach (var animeseries in list)
                {
                    var matches = TvDBLinkingHelper.GetTvDBEpisodeMatches(animeseries.AnimeID).Select(a => (
                        AniDB: new AniEpSummary
                        {
                            AniDBEpisodeType = a.AniDB.EpisodeType,
                            AniDBEpisodeNumber = a.AniDB.EpisodeNumber,
                            AniDBEpisodeName = a.AniDB.GetEnglishTitle()
                        },
                        TvDB: a.TvDB == null ? null : new TvDBEpSummary
                        {
                            TvDBSeason = a.TvDB.SeasonNumber,
                            TvDBEpisodeNumber = a.TvDB.EpisodeNumber,
                            TvDBEpisodeName = a.TvDB.EpisodeName
                        })).OrderBy(a => a.AniDB.AniDBEpisodeType).ThenBy(a => a.AniDB.AniDBEpisodeNumber).ToList();
                    var currentMatches = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeseries.AnimeID)
                        .Select(a =>
                        {
                            var AniDB = RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID);
                            var TvDB = RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID);
                            return (AniDB: new AniEpSummary
                                {
                                    AniDBEpisodeType = AniDB.EpisodeType,
                                    AniDBEpisodeNumber = AniDB.EpisodeNumber,
                                    AniDBEpisodeName = AniDB.GetEnglishTitle()
                                },
                                TvDB: TvDB == null ? null : new TvDBEpSummary
                                {
                                    TvDBSeason = TvDB.SeasonNumber,
                                    TvDBEpisodeNumber = TvDB.EpisodeNumber,
                                    TvDBEpisodeName = TvDB.EpisodeName
                                });
                        }).OrderBy(a => a.AniDB.AniDBEpisodeType).ThenBy(a => a.AniDB.AniDBEpisodeNumber).ToList();
                    if (!currentMatches.SequenceEqual(matches))
                    {
                        result.Add(new EpisodeMatchComparison
                        {
                            Anime = animeseries.MainTitle,
                            AnimeID = animeseries.AnimeID,
                            Current = currentMatches,
                            Calculated = matches,
                        });
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                logger.Error(e);
                return APIStatus.InternalError(e.Message);
            }
        }

        #endregion

        #region 06.MovieDB

        /// <summary>
        /// Scan MovieDB
        /// </summary>
        /// <returns></returns>
        private object ScanMovieDB()
        {
            Importer.RunImport_ScanMovieDB();
            return APIStatus.OK();
        }

        #endregion

        #region 07.User

        /// <summary>
        /// return Dictionary int = id, string = username
        /// </summary>
        /// <returns></returns>
        private object GetUsers()
        {
            return new CommonImplementation().GetUsers();
        }

        /// <summary>
        /// Create user from Contract_JMMUser
        /// </summary>
        /// <returns></returns>
        private object CreateUser()
        {
            SVR_JMMUser _user = (SVR_JMMUser) Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                JMMUser user = this.Bind();
                user.Password = Digest.Hash(user.Password);
                user.HideCategories = string.Empty;
                user.PlexUsers = string.Empty;
                return new ShokoServiceImplementation().SaveUser(user) == string.Empty
                    ? APIStatus.OK()
                    : APIStatus.InternalError();
            }

            return APIStatus.AdminNeeded();
        }

        /// <summary>
        ///  change current user password
        /// </summary>
        /// <returns></returns>
        private object ChangePassword()
        {
            SVR_JMMUser user = this.Bind();
            return ChangePassword(user.JMMUserID);
        }

        /// <summary>
        /// change given user (by uid) password
        /// </summary>
        /// <returns></returns>
        private object ChangePassword(int uid)
        {
            SVR_JMMUser thisuser = (SVR_JMMUser) Context.CurrentUser;
            SVR_JMMUser user = this.Bind();
            if (thisuser.IsAdmin == 1)
                return new ShokoServiceImplementation().ChangePassword(uid, user.Password) == string.Empty
                    ? APIStatus.OK()
                    : APIStatus.InternalError();
            if (thisuser.JMMUserID == user.JMMUserID)
                return new ShokoServiceImplementation().ChangePassword(uid, user.Password) == string.Empty
                    ? APIStatus.OK()
                    : APIStatus.InternalError();

            return APIStatus.AdminNeeded();
        }

        /// <summary>
        /// Delete user from his ID
        /// </summary>
        /// <returns></returns>
        private object DeleteUser()
        {
            SVR_JMMUser _user = (SVR_JMMUser) Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                SVR_JMMUser user = this.Bind();
                return new ShokoServiceImplementation().DeleteUser(user.JMMUserID) == string.Empty
                    ? APIStatus.OK()
                    : APIStatus.InternalError();
            }

            return APIStatus.AdminNeeded();
        }

        #endregion

        #region 8.OS-based operations

        /// <summary>
        /// Return OSFolder object that is a folder from which jmmserver is running
        /// </summary>
        /// <returns></returns>
        private object GetOSBaseFolder()
        {
            OSFolder dir = new OSFolder
            {
                full_path = Environment.CurrentDirectory
            };
            DirectoryInfo dir_info = new DirectoryInfo(dir.full_path);
            dir.dir = dir_info.Name;
            dir.subdir = new List<OSFolder>();

            foreach (DirectoryInfo info in dir_info.GetDirectories())
            {
                OSFolder subdir = new OSFolder
                {
                    full_path = info.FullName,
                    dir = info.Name
                };
                dir.subdir.Add(subdir);
            }
            return dir;
        }

        /// <summary>
        /// Return OSFolder object of directory that was given via
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        private object GetOSFolder(string folder)
        {
            OSFolder dir = this.Bind();
            if (!String.IsNullOrEmpty(dir.full_path))
            {
                DirectoryInfo dir_info = new DirectoryInfo(dir.full_path);
                dir.dir = dir_info.Name;
                dir.subdir = new List<OSFolder>();

                foreach (DirectoryInfo info in dir_info.GetDirectories())
                {
                    OSFolder subdir = new OSFolder
                    {
                        full_path = info.FullName,
                        dir = info.Name
                    };
                    dir.subdir.Add(subdir);
                }
                return dir;
            }

            return new APIMessage(400, "full_path missing");
        }

        /// <summary>
        /// Return OSFolder with subdirs as every driver on local system
        /// </summary>
        /// <returns></returns>
        private object GetOSDrives()
        {
            string[] drives = Directory.GetLogicalDrives();
            OSFolder dir = new OSFolder
            {
                dir = "/",
                full_path = "/",
                subdir = new List<OSFolder>()
            };
            foreach (string str in drives)
            {
                OSFolder driver = new OSFolder
                {
                    dir = str,
                    full_path = str
                };
                dir.subdir.Add(driver);
            }

            return dir;
        }

        #endregion

        #region 09.Cloud Accounts

        private object GetCloudAccounts()
        {
            // TODO APIv2: Cloud
            return APIStatus.NotImplemented();
        }

        private object GetCloudAccountsCount()
        {
            // TODO APIv2: Cloud
            return APIStatus.NotImplemented();
        }

        private object AddCloudAccount()
        {
            // TODO APIv2: Cloud
            return APIStatus.NotImplemented();
        }

        private object DeleteCloudAccount()
        {
            // TODO APIv2: Cloud
            return APIStatus.NotImplemented();
        }

        private object RunCloudImport()
        {
            ShokoServer.RunImport();
            return APIStatus.OK();
        }

        #endregion

        #region 10. Logs

        /// <summary>
        /// Run LogRotator with current settings
        /// </summary>
        /// <returns></returns>
        private object StartRotateLogs()
        {
            ShokoServer.logrotator.Start();
            return APIStatus.OK();
        }

        /// <summary>
        /// Set settings for LogRotator
        /// </summary>
        /// <returns></returns>
        private object SetRotateLogs()
        {
            Request request = Request;
            SVR_JMMUser user = (SVR_JMMUser) Context.CurrentUser;
            Logs rotator = this.Bind();

            if (user.IsAdmin == 1)
            {
                ServerSettings.RotateLogs = rotator.rotate;
                ServerSettings.RotateLogs_Zip = rotator.zip;
                ServerSettings.RotateLogs_Delete = rotator.delete;
                ServerSettings.RotateLogs_Delete_Days = rotator.days.ToString();

                return APIStatus.OK();
            }

            return APIStatus.AdminNeeded();
        }

        /// <summary>
        /// Get settings for LogRotator
        /// </summary>
        /// <returns></returns>
        private object GetRotateLogs()
        {
            Logs rotator = new Logs
            {
                rotate = ServerSettings.RotateLogs,
                zip = ServerSettings.RotateLogs_Zip,
                delete = ServerSettings.RotateLogs_Delete
            };
            int day = 0;
            if (!String.IsNullOrEmpty(ServerSettings.RotateLogs_Delete_Days))
            {
                int.TryParse(ServerSettings.RotateLogs_Delete_Days, out day);
            }
            rotator.days = day;

            return rotator;
        }

        /// <summary>
        /// return int position - current position
        /// return string[] lines - lines from current log file
        /// </summary>
        /// <param name="lines">max lines to return</param>
        /// <param name="position">position to seek</param>
        /// <returns></returns>
        private object GetLog(int lines, int position)
        {
            string log_file = LogRotator.GetCurrentLogFile();
            if (string.IsNullOrEmpty(log_file))
            {
                return APIStatus.NotFound("Could not find current log name. Sorry");
            }

            if (!File.Exists(log_file))
            {
                return APIStatus.NotFound();
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            FileStream fs = File.OpenRead(log_file);

            if (position >= fs.Length)
            {
                result.Add("position", fs.Length);
                result.Add("lines", new string[] { });
                return result;
            }

            List<string> logLines = new List<string>();

            LogReader reader = new LogReader(fs, position);
            for (int i = 0; i < lines; i++)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                logLines.Add(line);
            }
            result.Add("position", reader.Position);
            result.Add("lines", logLines.ToArray());
            return result;
        }

        #endregion

        #region 11. Image Actions

        private object UpdateImages()
        {
            Importer.RunImport_UpdateTvDB(true);
            ShokoServer.Instance.DownloadAllImages();

            return APIStatus.OK();
        }

        #endregion
    }
}
