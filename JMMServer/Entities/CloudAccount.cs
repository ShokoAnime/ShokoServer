using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using NutzCode.CloudFileSystem;
using NutzCode.CloudFileSystem.OAuth.Windows.WinForms;

namespace JMMServer.Entities
{
    public class CloudAccount
    {
        public int CloudID { get; private set; }
        public string ConnectionString { get; set; }
        public string Provider { get; set; }
        public string Name { get; set; }

        public BitmapImage Icon { get; private set; }
        private IFileSystem _fileSystem;
        public virtual IFileSystem FileSystem
        {
            get
            {
                if (_fileSystem == null)
                    Connect();
                return _fileSystem;
            }
        }



        public void Connect()
        {
            if (string.IsNullOrEmpty(Provider))
                throw new Exception("Empty provider supplied");
            Dictionary<string, object> auth = AuthorizationFactory.Instance.AuthorizationProvider.Get(Provider);
            if (auth == null)
                throw new Exception("Application Authorization Not Found");
            ICloudPlugin plugin = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == Provider);
            if (plugin == null)
                throw new Exception("Cannot find cloud provider '" + Provider + "'");
            Icon = plugin.CreateIconImage();
            FileSystemResult<IFileSystem> res = plugin.Init(Name, new AuthProvider(), auth, ConnectionString);
            if (!res.IsOk)
                throw new Exception("Unable to connect to '" + Provider + "'");
            _fileSystem = res.Result;
        }


    }
}
