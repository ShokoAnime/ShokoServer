using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
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
            get { return base.IsWatched; }
            set
            {
                base.IsWatched = this.SetField(base.IsWatched, value, () => IsWatched, () => FolderIsWatched);
            }
        }

        public new int IsDropSource
        {
            get { return base.IsDropSource; }
            set
            {
                base.IsDropSource = this.SetField(base.IsDropSource, value, () => IsDropSource, () => FolderIsDropSource);
            }
        }

       

        public new int IsDropDestination
        {
            get { return base.IsDropDestination; }
            set
            {
                base.IsDropDestination = this.SetField(base.IsDropDestination, value, ()=>IsDropDestination, ()=>FolderIsDropDestination);
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
        public BitmapSource Bitmap => _bitmap ?? (_bitmap = CloudID.HasValue ? CloudAccount.Bitmap : SVR_CloudAccount.CreateLocalFileSystemAccount().Bitmap);


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
        public SVR_CloudAccount CloudAccount => CloudID.HasValue ? RepoFactory.CloudAccount.GetByID(CloudID.Value) : null;


        public string CloudAccountName => CloudID.HasValue ? CloudAccount.Name : "Local FileSystem";

        public bool FolderIsWatched => IsWatched == 1;

        public bool FolderIsDropSource => IsDropSource == 1;
        public bool FolderIsDropDestination => IsDropDestination == 1;

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