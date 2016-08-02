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

namespace JMMServer.API
{
    //As responds for this API we throw object that will be converted to json/xml or standard http codes (HttpStatusCode)
    public class APIv2_Module : Nancy.NancyModule
    {
        //class will be found automagicly thanks to inherits also class need to be public (or it will 404)
        public APIv2_Module() : base("/api/")
        {
            Get["/"] = _ => { return IndexPage; };

            this.RequiresAuthentication();

            Get["/MyID"] = x => { return MyID(x.apikey); };
            Get["/GetFilters"] = _ => { return GetFilters(); };
            Get["/GetMetadata/{type}/{id}"] = x => { return GetMetadata(x.type, x.id); };

            // Images
            Get["/GetImage/{type}/{id}"] = parameter => { return GetImage(parameter.type, parameter.id); };
            Get["/GetImage/{type}/{id}/{thumb}"] = parameter => { return GetImage(parameter.id, parameter.type, parameter.thumb); };
            Get["/GetThumb/{type}/{id}/{ratio}"] = parameter => { return GetThumb(parameter.type, parameter.id, parameter.ratio); };
            Get["/GetSupportImage/{name}"] = parameter => { return GetSupportImage(parameter.name); };
            Get["/GetSupportImage/{name}/{ratio}"] = parameter => { return GetSupportImage(parameter.name, parameter.ratio); };
            Get["/GetImageUsingPath/{path}"] = parameter => { return GetImageUsingPath(parameter.path); };

            // Operations on collection
            Get["/ListFolders"] = x => { return ListFolders(); };
            Post["/AddFolder"] = x => { return AddFolder(); };
            Post["/DeleteFolder"] = x => { return DeleteFolder(); };
            Post["/AddUPNP"] = x => { return AddUPNP(); };
            Post["/RunImport"] = _ => { return RunImport(); };
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

        private object ListFolders()
        {
            List<Contract_ImportFolder> list = new JMMServiceImplementation().GetImportFolders();
            return list;
        }

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

        private object AddUPNP()
        {
            //TODO APIv2: implement this
            throw new NotImplementedException();
        }

        private object RunImport()
        {
            MainWindow.RunImport();
            return HttpStatusCode.OK;
        }
        
        #endregion
    }
}
