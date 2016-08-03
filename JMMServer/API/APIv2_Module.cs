using Nancy;
using JMMServer.PlexAndKodi;
using JMMServer.PlexAndKodi.Kodi;
using Nancy.Security;
using System;
using JMMServer.API.Model;
using Nancy.ModelBinding;
using JMMServer.Entities;
using JMMContracts;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using JMMServer.Commands;

namespace JMMServer.API
{
    //As responds for this API we throw object that will be converted to json/xml or standard http codes (HttpStatusCode)
    public class APIv2_Module : Nancy.NancyModule
    {
        //class will be found automagicly thanks to inherits also class need to be public (or it will 404)
        //routes are named with twitter api style
        //every function with summary is implemented 
        public APIv2_Module() : base("/api/")
        {
            Get["/"] = _ => { return IndexPage; };

            this.RequiresAuthentication();

            Get["/MyID"] = x => { return MyID(x.apikey); };
            Get["/GetFilters"] = _ => { return GetFilters(); };
            Get["/GetMetadata/{type}/{id}"] = x => { return GetMetadata(x.type, x.id); };

            // Images
            Get["/get_image/{type}/{id}"] = parameter => { return GetImage(parameter.type, parameter.id); };
            Get["/get_image/{type}/{id}/{thumb}"] = parameter => { return GetImage(parameter.id, parameter.type, parameter.thumb); };
            Get["/get_thumb/{type}/{id}/{ratio}"] = parameter => { return GetThumb(parameter.type, parameter.id, parameter.ratio); };
            Get["/get_support_image/{name}"] = parameter => { return GetSupportImage(parameter.name); };
            Get["/get_support_image/{name}/{ratio}"] = parameter => { return GetSupportImage(parameter.name, parameter.ratio); };
            Get["/get_image_using_path/{path}"] = parameter => { return GetImageUsingPath(parameter.path); };

            // Operations on collection
            Get["/folder/list"] = x => { return ListFolders(); };
            Post["/folder/add"] = x => { return AddFolder(); };
            Post["/folder/delete"] = x => { return DeleteFolder(); };
            Post["/upnp/list"] = x => { return ListUPNP(); };
            Post["/upnp/add"] = x => { return AddUPNP(); };
            Post["/upnp/delete"] = x => { return DeleteUPNP(); };
            Get["/import"] = _ => { return RunImport(); };

            // Settings
            Post["/port/set"] = _ => { return SetPort(); };
            Get["/port/get"] = _ => { return GetPort(); };
            Post["/imagepath/set"] = _ => { return SetImagepath(); };
            Get["/imagepath/get"] = _ => { return GetImagepath(); };
            Post["/anidb/set"] = _ => { return SetAniDB(); };
            Get["/anidb/get"] = _ => { return TestAniDB(); };
            Post["/mal/set"] = _ => { return SetMAL(); };
            Get["/mal/get"] = _ => { return TestMAL(); };
            Post["/trakt/set"] = _ => { return SetTrakt(); };
            Get["/trakt/get"] = _ => { return TestTrakt(); };

            // Setup
            Post["/db/set"] = _ => { return SetupDB(); };
            Get["/db/get"] = _ => { return GetDB(); };

            // Actions
            Get["/remove_missing_files"] = _ => { return RemoveMissingFiles(); };
            Get["/stats_update"] = _ => { return UpdateStats(); };
            Get["/mediainfo_update"] = _ => { return UpdateMediaInfo(); };
            Get["/hash/sync"] = _ => { return HashSync(); };
            Get["/trakt/sync"] = _ => { return SyncTrakt(); };
            Get["/trakt/update"] = _ => { return UpdateAllTrakt(); };
            Get["/anidb/votes/sync"] = _ => { return SyncAniDBVotes(); };
            Get["/anidb/list/sync"] = _ => { return SyncAniDBList(); };
            Get["/anidb/update"] = _ => { return UpdateAllAniDB(); };
            Get["/mal/votes/sync"] = _ => { return SyncMALVotes(); };            
            Get["/tvdb/update"] = _ => { return UpdateAllTvDB(); };
        }

        CommonImplementation _impl = new CommonImplementation();
        IProvider _prov_kodi = new KodiProvider();
        JMMServiceImplementationREST _rest = new JMMServiceImplementationREST();

        const String IndexPage = @"<html><body><h1>JMMServer is running</h1></body></html>";

        //return userid as it can be needed in legacy implementation
        private object MyID(string s)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                return " { \"userid\":\"" + user.JMMUserID.ToString() + "\" }";
            }
            else
            {
                return null;
            }
        }

        private object GetFilters()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                return _impl.GetFilters(_prov_kodi, user.JMMUserID.ToString());
            }
            else
            {
                return new JMMContracts.PlexAndKodi.Response();
            }
        }

        private object GetMetadata(string typeid, string id)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                return _impl.GetMetadata(_prov_kodi, user.JMMUserID.ToString(), typeid, id, null);
            }
            else
            {
                return new JMMContracts.PlexAndKodi.Response();
            }
        }

        #region Images

        /// <summary>
        ///  Return image that is used as support image, images are build-in 
        /// </summary>
        /// <param name="name">name of image inside resource</param>
        /// <returns></returns>
        private object GetSupportImage(string name)
        {
            using (System.IO.Stream image = _impl.GetSupportImage(name))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return image that is used as support image, images are build-in with given ratio
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetSupportImage(string name, string ratio)
        {
            using (System.IO.Stream image = _rest.GetSupportImage(name, ratio))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return image with given path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private object GetImageUsingPath(string path)
        {
            using (System.IO.Stream image = _rest.GetImageUsingPath(path))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return image with given type and id
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private object GetImage(string type, string id)
        {
            return GetImage(type, id, false);
        }

        /// <summary>
        /// Return image with given type, id and if this should be a thumbnail
        /// </summary>
        /// <param name="id"></param>
        /// <param name="type"></param>
        /// <param name="thumb"></param>
        /// <returns></returns>
        private object GetImage(string type, string id, bool thumb)
        {
            using (System.IO.Stream image = _rest.GetImage(type, id, thumb))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        /// <summary>
        /// Return thumbnail from given type and id with ratio
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        private object GetThumb(string type, string id, string ratio)
        {
            using (System.IO.Stream image = _rest.GetThumb(type, id, ratio))
            {
                Nancy.Response response = new Nancy.Response();
                response = Response.FromStream(image, "image/png");
                return response;
            }
        }

        #endregion

        #region Operations on collection

        /// <summary>
        /// List all saved Import Folders
        /// </summary>
        /// <returns></returns>
        private object ListFolders()
        {
            List<Contract_ImportFolder> list = new JMMServiceImplementation().GetImportFolders();
            return list;
        }

        /// <summary>
        /// Add Folder to Import Folders Repository
        /// </summary>
        /// <returns></returns>
        private object AddFolder()
        {
            ImportFolder folder = this.Bind();
            if (folder.ImportFolderLocation != "")
            {
                try
                {
                    if (folder.IsDropDestination == 1 && folder.IsDropSource == 1)
                    {
                        return HttpStatusCode.Conflict;
                    }
                    else
                    {
                        Contract_ImportFolder_SaveResponse response = new JMMServiceImplementation().SaveImportFolder(folder.ToContract());

                        if (!string.IsNullOrEmpty(response.ErrorMessage))
                        {
                            return HttpStatusCode.InternalServerError;
                        }

                        return HttpStatusCode.OK;
                    }
                }
                catch
                {
                    return HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Delete Import Folder out of Import Folder Repository
        /// </summary>
        /// <returns></returns>
        private object DeleteFolder()
        {
            ImportFolder folder = this.Bind();
            if (folder.ImportFolderID != 0)
            {
                if (Importer.DeleteImportFolder(folder.ImportFolderID) == "")
                {
                    return HttpStatusCode.OK;
                }
                else
                {
                    return HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        private object ListUPNP()
        {
            //TODO APIv2: implement this
            throw new NotImplementedException();
        }

        private object AddUPNP()
        {
            //TODO APIv2: implement this
            throw new NotImplementedException();
        }

        private object DeleteUPNP()
        {
            //TODO APIv2: implement this
            throw new NotImplementedException();
        }

        /// <summary>
        /// Run Import action on all Import Folders inside Import Folders Repository
        /// </summary>
        /// <returns></returns>
        private object RunImport()
        {
            MainWindow.RunImport();
            return HttpStatusCode.OK;
        }

        #endregion

        #region Settings

        /// <summary>
        /// Set JMMServer Port
        /// </summary>
        /// <returns></returns>
        private object SetPort()
        {
            Creditentials cred = this.Bind();
            if (cred.port != 0)
            {
                ServerSettings.JMMServerPort = cred.port.ToString();
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Get JMMServer Port
        /// </summary>
        /// <returns></returns>
        private object GetPort()
        {
            return ServerSettings.JMMServerPort;
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
                ServerSettings.BaseImagesPathIsDefault = imagepath.isdefault;
                return HttpStatusCode.OK;
            }
            else
            {
                if (String.IsNullOrEmpty(imagepath.path) && imagepath.path != "")
                {
                    ServerSettings.BaseImagesPath = imagepath.path;
                    return HttpStatusCode.OK;
                }
                else
                {
                    return HttpStatusCode.BadRequest;
                }
            }
        }

        /// <summary>
        /// Return ImagePath object
        /// </summary>
        /// <returns></returns>
        private object GetImagepath()
        {
            ImagePath imagepath = new ImagePath();
            imagepath.path = ServerSettings.BaseImagesPath;
            imagepath.isdefault = ServerSettings.BaseImagesPathIsDefault;

            return imagepath;
        }

        /// <summary>
        /// Set AniDB account with login, password and client port
        /// </summary>
        /// <returns></returns>
        private object SetAniDB()
        {
            Creditentials cred = this.Bind();
            if (String.IsNullOrEmpty(cred.login) && cred.login != "" && String.IsNullOrEmpty(cred.password) && cred.password != "")
            {
                ServerSettings.AniDB_Username = cred.login;
                ServerSettings.AniDB_Password = cred.password;
                if (cred.port != 0)
                {
                    ServerSettings.AniDB_ClientPort = cred.port.ToString();
                }
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Test AniDB Creditentials
        /// </summary>
        /// <returns></returns>
        private object TestAniDB()
        {
            JMMService.AnidbProcessor.ForceLogout();
            JMMService.AnidbProcessor.CloseConnections();

            Thread.Sleep(1000);

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                ServerSettings.AniDB_ServerAddress,
                ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

            if (JMMService.AnidbProcessor.Login())
            {
                JMMService.AnidbProcessor.ForceLogout();
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.Forbidden;
            }
        }

        /// <summary>
        /// Set MAL account with login, password
        /// </summary>
        /// <returns></returns>
        private object SetMAL()
        {
            Creditentials cred = this.Bind();
            if (String.IsNullOrEmpty(cred.login) && cred.login != "" && String.IsNullOrEmpty(cred.password) && cred.password != "")
            {
                ServerSettings.MAL_Username = cred.login;
                ServerSettings.MAL_Password = cred.password;
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        private object TestMAL()
        {
            //TODO APIv2: implement this
            throw new NotImplementedException();
        }

        /// <summary>
        /// Set Trakt Token
        /// </summary>
        /// <returns></returns>
        private object SetTrakt()
        {
            Creditentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.token) && cred.token != "")
            {
                ServerSettings.Trakt_AuthToken = cred.token;
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        private object TestTrakt()
        {
            //TODO APIv2: implement this
            throw new NotImplementedException();
        }

        #endregion

        #region Setup

        /// <summary>
        /// Setup Database and Init it
        /// </summary>
        /// <returns></returns>
        private object SetupDB()
        {
            Database db = this.Bind();
            if (String.IsNullOrEmpty(db.type) && db.type != "")
            {
                switch (db.type.ToLower())
                {
                    case "sqlite":
                        ServerSettings.DatabaseFile = db.path;
                        break;

                    case "sqlserver":
                        ServerSettings.DatabaseUsername = db.login;
                        ServerSettings.DatabasePassword = db.password;
                        ServerSettings.DatabaseName = db.table;
                        ServerSettings.DatabaseServer = db.server;
                        break;

                    case "mysql":
                        ServerSettings.MySQL_Username = db.login;
                        ServerSettings.MySQL_Password = db.password;
                        ServerSettings.MySQL_SchemaName = db.table;
                        ServerSettings.MySQL_Hostname = db.server;
                        break;
                }

                MainWindow.workerSetupDB.RunWorkerAsync();
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Return Database object
        /// </summary>
        /// <returns></returns>
        private object GetDB()
        {
            Database db = this.Bind();
            db.type = ServerSettings.DatabaseType;
            if (String.IsNullOrEmpty(db.type) && db.type != "")
            {
                switch (db.type.ToLower())
                {
                    case "sqlite":
                        db.path = ServerSettings.DatabaseFile;
                        break;

                    case "sqlserver":
                        db.login = ServerSettings.DatabaseUsername;
                        db.password = ServerSettings.DatabasePassword;
                        db.table = ServerSettings.DatabaseName;
                        db.server = ServerSettings.DatabaseServer;
                        break;

                    case "mysql":
                        db.login = ServerSettings.MySQL_Username;
                        db.password = ServerSettings.MySQL_Password;
                        db.table = ServerSettings.MySQL_SchemaName;
                        db.server = ServerSettings.MySQL_Hostname;
                        break;
                }

                return db;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }       
        }

        #endregion

        #region Actions

        /// <summary>
        /// Scans your import folders and remove files from your database that are no longer in your collection.
        /// </summary>
        /// <returns></returns>
        private object RemoveMissingFiles()
        {
            MainWindow.RemoveMissingFiles();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Updates all series stats such as watched state and missing files.
        /// </summary>
        /// <returns></returns>
        private object UpdateStats()
        {
            Importer.UpdateAllStats();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Updates all technical details about the files in your collection via running MediaInfo on them.
        /// </summary>
        /// <returns></returns>
        private object UpdateMediaInfo()
        {
            MainWindow.RefreshAllMediaInfo();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Sync Hashes - download/upload hashes from/to webcache
        /// </summary>
        /// <returns></returns>
        private object HashSync()
        {
            MainWindow.SyncHashes();
            return HttpStatusCode.OK;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.NoContent;
            }
        }

        /// <summary>
        /// Update All information from Trakt
        /// </summary>
        /// <returns></returns>
        private object UpdateAllTrakt()
        {
            Providers.TraktTV.TraktTVHelper.UpdateAllInfo();
            return HttpStatusCode.OK;
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
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Sync AniDB List
        /// </summary>
        /// <returns></returns>
        private object SyncAniDBList()
        {
            MainWindow.SyncMyList();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Update all series infromation from AniDB
        /// </summary>
        /// <returns></returns>
        private object UpdateAllAniDB()
        {
            Importer.RunImport_UpdateAllAniDB();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Sync votes bettween Local and AniDB and only upload to MAL
        /// </summary>
        /// <returns></returns>
        private object SyncMALVotes()
        {
            //TODO APIv2: Command should be split into AniDb/MAL sepereate
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Update all information from TvDB
        /// </summary>
        /// <returns></returns>
        private object UpdateAllTvDB()
        {
            Importer.RunImport_UpdateTvDB(false);
            return HttpStatusCode.OK;
        }


        #endregion
    }
}
