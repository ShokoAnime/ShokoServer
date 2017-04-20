using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Notification;
using Shoko.Models.Server;
using Shoko.Server.Repositories;
using Pri.LongPath;

namespace Shoko.Server.Models
{
    public class SVR_ImportFolder : ImportFolder, INotifyPropertyChangedExt
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public new int IsWatched
        {
            get { return base.IsWatched; }
            set
            {
                this.SetField(() => base.IsWatched, (r) => base.IsWatched = r, value, () => IsWatched,
                    () => FolderIsWatched);
            }
        }

        public void Bass<T>(Expression<Func<T, int>> step, int value)
        {
        }

        public void Bass2<T>(Func<int> test, int value)
        {
        }

        public new int IsDropSource
        {
            get { return base.IsDropSource; }
            set
            {
                this.SetField(() => base.IsDropSource, (r) => base.IsDropSource = r, value, () => IsDropSource,
                    () => FolderIsDropSource);
            }
        }


        public new int IsDropDestination
        {
            get { return base.IsDropDestination; }
            set
            {
                this.SetField(() => base.IsDropDestination, (r) => base.IsDropDestination = r, value,
                    () => IsDropDestination, () => FolderIsDropDestination);
            }
        }

        public new string ImportFolderLocation
        {
            get { return base.ImportFolderLocation; }
            set
            {
                string nvalue = value;
                if (nvalue != null)
                {
                    if (nvalue.EndsWith(":"))
                        nvalue += Path.DirectorySeparatorChar;
                    if (nvalue.Length > 0 && nvalue.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    {
                        while (nvalue.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        {
                            nvalue = nvalue.Substring(0, nvalue.Length - 1);
                        }
                    }
                    nvalue += Path.DirectorySeparatorChar;
                }
                base.ImportFolderLocation = nvalue;
            }
        }


        private IFileSystem _filesystem;

        internal IFileSystem FileSystem
        {
            get
            {
                if (_filesystem == null)
                {
                    if (CloudID != null)
                    {
                        SVR_CloudAccount cl = RepoFactory.CloudAccount.GetByID(CloudID.Value);
                        if (cl == null)
                            throw new Exception("Cloud Account Not Found");
                        _filesystem = cl.FileSystem;
                    }
                    else
                    {
                        FileSystemResult<IFileSystem> ff = CloudFileSystemPluginFactory.Instance.List
                            .FirstOrDefault(a => a.Name == "Local File System")
                            ?.Init("", null, null);
                        if (ff == null || !ff.IsOk)
                            throw new Exception(ff?.Error ?? "Error Opening Local Filesystem");
                        _filesystem = ff.Result;
                    }
                }

                return _filesystem;
            }
        }

        private BitmapSource _bitmap;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public BitmapSource Bitmap
        {
            get
            {
                if (_bitmap != null)
                {
                    return _bitmap;
                }
                _bitmap = CloudID.HasValue
                    ? CloudAccount.Bitmap
                    : SVR_CloudAccount.CreateLocalFileSystemAccount().Bitmap;

                return _bitmap;
            }
        }


        internal IDirectory BaseDirectory
        {
            get
            {
                FileSystemResult<IObject> fr = FileSystem.Resolve(ImportFolderLocation);

                if (fr.IsOk && fr.Result is IDirectory)
                    return (IDirectory) fr.Result;
                throw new Exception("Import Folder not found '" + ImportFolderLocation + "'");
            }
        }

        public SVR_CloudAccount CloudAccount
        {
            get { return CloudID.HasValue ? RepoFactory.CloudAccount.GetByID(CloudID.Value) : null; }
        }


        internal string CloudAccountName
        {
            get { return CloudID.HasValue ? CloudAccount.Name : "Local FileSystem"; }
        }

        internal bool FolderIsWatched => IsWatched == 1;

        internal bool FolderIsDropSource => IsDropSource == 1;
        internal bool FolderIsDropDestination => IsDropDestination == 1;

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2})", ImportFolderName, ImportFolderLocation, ImportFolderID);
        }

        public SVR_ImportFolder()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }
    }
}