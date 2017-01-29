using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Notification;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_ImportFolder : ImportFolder, INotifyPropertyChangedExt
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public new int IsWatched
        {
            get
            {
                logger.Info("IsWatched");
                return base.IsWatched;
            }
            set
            {
                base.IsWatched = this.SetField(base.IsWatched, value, () => IsWatched, () => FolderIsWatched);
            }
        }

        public new int IsDropSource
        {
            get
            {
                logger.Info("IsDrop");

                return base.IsDropSource;
            }
            set
            {
                base.IsDropSource = this.SetField(base.IsDropSource, value, () => IsDropSource, () => FolderIsDropSource);
            }
        }

       

        public new int IsDropDestination
        {
            get
            {
                logger.Info("IsDropDest");

                return base.IsDropDestination;
            }
            set
            {
                base.IsDropDestination = this.SetField(base.IsDropDestination, value, ()=>IsDropDestination, ()=>FolderIsDropDestination);
            }
        }
        public new string ImportFolderLocation
        {
            get
            {
                logger.Info("ImportFolderLocation");

                return base.ImportFolderLocation;
            }
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
                logger.Info("FileSys");

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
                        FileSystemResult<IFileSystem> ff= CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == "Local File System")?.Init("", null, null);
                        if (ff==null || !ff.IsOk)
                            throw new Exception(ff?.Error ?? "Error Opening Local Filesystem");
                        _filesystem = ff.Result;
                    }
                }
                logger.Info("EndFileSys");
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
                logger.Info("Bitmap");
                if (_bitmap != null)
                {
                    logger.Info("Bitmap already");
                    return _bitmap;

                }
                _bitmap = CloudID.HasValue ? CloudAccount.Bitmap : SVR_CloudAccount.CreateLocalFileSystemAccount().Bitmap;
                logger.Info("New Bitmap");
                return _bitmap;
            }

        }


        internal IDirectory BaseDirectory
        {
            get
            {
                logger.Info("BaseDir");
                FileSystemResult<IObject> fr = FileSystem.Resolve(ImportFolderLocation);
                logger.Info("EndBaseDir");
                if (fr.IsOk && fr.Result is IDirectory)
                    return (IDirectory)fr.Result;
                throw new Exception("Import Folder not found '" + ImportFolderLocation + "'");
            }
        }
        internal SVR_CloudAccount CloudAccount
        {
            get
            {
                logger.Info("CloudAccount");
                return CloudID.HasValue ? RepoFactory.CloudAccount.GetByID(CloudID.Value) : null;
            }
        }


        internal string CloudAccountName
        {
            get
            {

                logger.Info("CloudAccountName");

                return CloudID.HasValue ? CloudAccount.Name : "Local FileSystem";
            }
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