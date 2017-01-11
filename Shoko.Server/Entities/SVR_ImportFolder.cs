using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Entities
{
    public class SVR_ImportFolder : ImportFolder, INotifyPropertyChanged
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public new int IsWatched
        {
            get { return base.IsWatched; }
            set
            {
                base.IsWatched = value;
                NotifyPropertyChanged("IsWatched");
                FolderIsWatched = base.IsWatched == 1;
            }
        }

        public new int IsDropSource
        {
            get { return base.IsDropSource; }
            set
            {
                base.IsDropSource = value;
                NotifyPropertyChanged("IsDropSource");
                FolderIsDropSource = IsDropSource == 1;
            }
        }

       

        public int IsDropDestination
        {
            get { return base.IsDropDestination; }
            set
            {
                base.IsDropDestination = value;
                NotifyPropertyChanged("IsDropDestination");
                FolderIsDropDestination = IsDropDestination == 1;
            }
        }
        public new string ImportFolderLocation
        {
            get { return base.ImportFolderLocation; }
            set
            {
                string nvalue = value;
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
                base.ImportFolderLocation = nvalue;
            }
        }


        private IFileSystem _filesystem;

        public IFileSystem FileSystem
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
                        FileSystemResult<IFileSystem> ff= CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == "Local File System")?.Init("", null, null);
                        if (ff==null || !ff.IsOk)
                            throw new Exception(ff?.Error ?? "Error Opening Local Filesystem");
                        _filesystem = ff.Result;
                    }
                }
                return _filesystem;
            }
        }

        private BitmapSource _bitmap;
        public BitmapSource Bitmap
        {
            get
            {
                if (_bitmap==null)
                    _bitmap = CloudID.HasValue ? CloudAccount.Bitmap : SVR_CloudAccount.CreateLocalFileSystemAccount().Bitmap;
                return _bitmap;
            }
        }


        public IDirectory BaseDirectory
        {
            get
            {
                FileSystemResult<IObject> fr = FileSystem.Resolve(ImportFolderLocation);
                if (fr.IsOk && fr.Result is IDirectory)
                    return (IDirectory)fr.Result;
                throw new Exception("Import Folder not found '" + ImportFolderLocation + "'");
            }
        }
        public SVR_CloudAccount CloudAccount
        {
            get
            {
                if (CloudID.HasValue)
                {
                    return RepoFactory.CloudAccount.GetByID(CloudID.Value);
                }
                return null;
            }
        }



        public string CloudAccountName
        {
            get
            {
                if (CloudID.HasValue)
                    return CloudAccount.Name;
                return "Local FileSystem";
            }
        }
        private Boolean folderIsWatched = true;

        public Boolean FolderIsWatched
        {
            get { return folderIsWatched; }
            set
            {
                folderIsWatched = value;
                NotifyPropertyChanged("FolderIsWatched");
            }
        }

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2})", ImportFolderName, ImportFolderLocation, ImportFolderID);
        }

        public SVR_ImportFolder()
        {
            FolderIsDropSource = IsDropSource == 1;
            FolderIsDropDestination = IsDropDestination == 1;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String propertyName)
        {
            if (PropertyChanged != null)
            {
                var args = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, args);
            }
        }

        private Boolean folderIsDropSource = true;

        public Boolean FolderIsDropSource
        {
            get { return folderIsDropSource; }
            set
            {
                folderIsDropSource = value;
                NotifyPropertyChanged("FolderIsDropSource");
            }
        }

        private Boolean folderIsDropDestination = true;

        public Boolean FolderIsDropDestination
        {
            get { return folderIsDropDestination; }
            set
            {
                folderIsDropDestination = value;
                NotifyPropertyChanged("FolderIsDropDestination");
            }
        }
    }
}