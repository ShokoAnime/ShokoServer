using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NutzCode.CloudFileSystem;
using NutzCode.CloudFileSystem.OAuth2;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_CloudAccount : CloudAccount
    {
        //private static AuthorizationFactory _cache; //lazy init, because 
        private ICloudPlugin _plugin;


        [NotMapped]
        public new string Provider
        {
            get { return base.Provider; }
            set
            {
                base.Provider = value;
                if (!string.IsNullOrEmpty(value))
                {
                    _plugin = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == value);
                }
            }
        }

        [JsonIgnore]
        [XmlIgnore]
        [NotMapped]
        public byte[] Bitmap => _plugin?.Icon;

        [JsonIgnore]
        [XmlIgnore]
        [NotMapped]
        public byte[] Icon => _plugin?.Icon;

        [NotMapped]
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
                        Repo.CloudAccount.Touch(() => this);
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

        [NotMapped]
        [JsonIgnore]
        [XmlIgnore]
        internal bool NeedSave { get; set; } = false;

        /*private static AuthorizationFactory _cache; //lazy init, because 
        private static AuthorizationFactory AuthInstance
        {
            get { return new AuthorizationFactory("AppGlue.dll"); }
        }*/

        public IFileSystem Connect()
        {
            return Connect(null, null);
        }

        public IFileSystem Connect(string code, string uri)
        {
            if (string.IsNullOrEmpty(Provider))
                throw new Exception("Empty provider supplied");

            //TODO: 
            Dictionary<string, object> auth = default;//AuthInstance.AuthorizationProvider.Get(Provider);
            if (auth == null)
                throw new Exception("Application Authorization Not Found");
            _plugin = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == Provider);
            if (_plugin == null)
                throw new Exception("Cannot find cloud provider '" + Provider + "'");
          
            LocalUserSettings userSettings = new LocalUserSettings();
            if (ConnectionString != string.Empty)
            {
                userSettings = new LocalUserSettingWithCode();
                ((LocalUserSettingWithCode) userSettings).Code = code;
                ((LocalUserSettingWithCode) userSettings).OriginalRedirectUri = uri;
            }
            if (auth.ContainsKey("ClientId"))
                userSettings.ClientId = (string)auth["ClientId"];
            if (auth.ContainsKey("ClientSecret"))
                userSettings.ClientSecret = (string)auth["ClientSecret"];
            if (auth.ContainsKey("Scopes"))
                userSettings.Scopes = (List<string>)auth["Scopes"];
            if (auth.ContainsKey("UserAgent"))
                userSettings.UserAgent = (string)auth["UserAgent"];
            if (auth.ContainsKey("AcknowledgeAbuse"))
                userSettings.AcknowledgeAbuse = (bool)auth["AcknowledgeAbuse"];
            if (auth.ContainsKey("ClientAppFriendlyName"))
                userSettings.ClientAppFriendlyName = (string)auth["ClientAppFriendlyName"];
            IFileSystem res = _plugin.Init(Name, userSettings, ConnectionString);
            if (res.Status == Status.Ok)
            {
                string userauth = res.GetUserAuthorization();
                if (ConnectionString != userauth)
                {
                    NeedSave = true;
                    ConnectionString = userauth;
                }
            }

            return res;
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