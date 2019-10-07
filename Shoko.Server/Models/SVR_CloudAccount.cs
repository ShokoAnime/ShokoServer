using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NutzCode.CloudFileSystem;
using NutzCode.CloudFileSystem.OAuth2;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

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
                    _plugin = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name.EqualsInvariantIgnoreCase(value));
                }
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public byte[] Bitmap => _plugin?.Icon;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public byte[] Icon => _plugin?.Icon;
        private ICloudPlugin _plugin;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public IFileSystem FileSystem
        {
            get
            {
                if (!ServerState.Instance.ConnectedFileSystems.ContainsKey(Name))
                {
                    ServerState.Instance.ConnectedFileSystems[Name] = Connect();
                    if (NeedSave)
                        RepoFactory.CloudAccount.Save(this);
                }
                return ServerState.Instance.ConnectedFileSystems[Name];
            }
            set
            {
                if (value != null)
                    ServerState.Instance.ConnectedFileSystems[Name] = value;
                else if (ServerState.Instance.ConnectedFileSystems.ContainsKey(Name))
                    ServerState.Instance.ConnectedFileSystems.Remove(Name);
            }
        }

        public bool IsConnected
        {
            get { return ServerState.Instance.ConnectedFileSystems.ContainsKey(Name ?? string.Empty); }
        }

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        internal bool NeedSave { get; set; } = false;

        private static AuthorizationFactory _cache; //lazy init, because 
        private static AuthorizationFactory AuthInstance
        {
            get { return new AuthorizationFactory("AppGlue.dll"); }
        }

        public IFileSystem Connect()
        {
            if (string.IsNullOrEmpty(Provider))
                throw new Exception("Empty provider supplied");

            Dictionary<string, object> auth = AuthInstance.AuthorizationProvider.Get(Provider);
            if (auth == null)
                throw new Exception("Application Authorization Not Found");
            _plugin = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name.EqualsInvariantIgnoreCase(Provider));
            if (_plugin == null)
                throw new Exception("Cannot find cloud provider '" + Provider + "'");
            FileSystemResult<IFileSystem> res = _plugin.Init(Name, ShokoServer.Instance.OAuthProvider, auth, ConnectionString);
            if (res == null || !res.IsOk)
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