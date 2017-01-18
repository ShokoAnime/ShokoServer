using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using NutzCode.CloudFileSystem;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using AuthProvider = Shoko.Server.UI.AuthProvider;

namespace Shoko.Server.Models
{
    public class SVR_CloudAccount : CloudAccount
    {
        public SVR_CloudAccount()
        {
        }
        public new string Provider 
        {
            get { return base.Provider; }
            set
            {
                base.Provider = value;
                if (!string.IsNullOrEmpty(value))
                {
                    _plugin = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name ==value);
                    if (_plugin != null)
                    {
                        Bitmap = _plugin.CreateIconImage();
                    }
                }
            }
        }
        public BitmapImage Bitmap { get;  set; }
        public byte[] Icon => _plugin?.Icon;
        private ICloudPlugin _plugin;


        public virtual IFileSystem FileSystem
        {
            get
            {
                if (!ServerState.Instance.ConnectedFileSystems.ContainsKey(Name))
                {
                    ServerState.Instance.ConnectedFileSystems[Name] = Connect(MainWindow.Instance);
                    if (NeedSave)
                        RepoFactory.CloudAccount.Save(this);
                }
                return ServerState.Instance.ConnectedFileSystems[Name];
            }
            set
            {
                if (value!=null)
                    ServerState.Instance.ConnectedFileSystems[Name] = value;
                else if (ServerState.Instance.ConnectedFileSystems.ContainsKey(Name))
                    ServerState.Instance.ConnectedFileSystems.Remove(Name);
                    
            }
        }
        public bool IsConnected => ServerState.Instance.ConnectedFileSystems.ContainsKey(Name ?? string.Empty);


        public bool NeedSave { get; set; } = false;


        private static AuthorizationFactory AuthInstance = new AuthorizationFactory("AppGlue.dll");

        public IFileSystem Connect(Window owner)
        {
            if (string.IsNullOrEmpty(Provider))
                throw new Exception("Empty provider supplied");

            Dictionary<string, object> auth = AuthInstance.AuthorizationProvider.Get(Provider);
            if (auth == null)
                throw new Exception("Application Authorization Not Found");
            _plugin = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == Provider);
            if (_plugin == null)
                throw new Exception("Cannot find cloud provider '" + Provider + "'");
            Bitmap = _plugin.CreateIconImage();
            FileSystemResult<IFileSystem> res = _plugin.Init(Name, new UI.AuthProvider(owner), auth, ConnectionString);            
            if (!res.IsOk)
                throw new Exception("Unable to connect to '" + Provider + "'");
            string userauth = res.Result.GetUserAuthorization();
            if (ConnectionString != userauth)
            {
                NeedSave = true;
                ConnectionString = userauth;
            }
            return res.Result;
        }
        public static SVR_CloudAccount CreateLocalFileSystemAccount()
        {
            return new SVR_CloudAccount
            {
                Name = "NA",
                Provider = "Local File System"
            };
        }


    }
}
