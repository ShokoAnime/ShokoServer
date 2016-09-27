using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using NLog;
using NutzCode.CloudFileSystem;


namespace JMMServer.Entities
{
    public class ImportFolder : INotifyPropertyChanged
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int ImportFolderID { get; set; }
        public int ImportFolderType { get; set; }
        public string ImportFolderName { get; set; }
        public string ImportFolderLocation { get; set; }
        public int? CloudID { get; set; }

        private int isWatched = 0;


        private IFileSystem _filesystem;

        public IFileSystem FileSystem
        {
            get
            {
                if (_filesystem == null)
                {
                    if (CloudID != null)
                    {
                        CloudAccount cl = RepoFactory.CloudAccount.GetByID(CloudID.Value);
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

        private BitmapSource _icon;
        public BitmapSource Icon
        {
            get
            {
                if (_icon==null)
                    _icon = CloudID.HasValue ? CloudAccount.Icon : new CloudAccount() {Name = "NA", Provider = "Local File System"}.Icon;
                return _icon;
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
        public CloudAccount CloudAccount
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

        public int IsWatched
        {
            get { return isWatched; }
            set
            {
                isWatched = value;
                NotifyPropertyChanged("IsWatched");
                FolderIsWatched = IsWatched == 1;
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

        public ImportFolder()
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

        private int isDropSource = 0;

        public int IsDropSource
        {
            get { return isDropSource; }
            set
            {
                isDropSource = value;
                NotifyPropertyChanged("IsDropSource");
                FolderIsDropSource = IsDropSource == 1;
            }
        }

        private int isDropDestination = 0;

        public int IsDropDestination
        {
            get { return isDropDestination; }
            set
            {
                isDropDestination = value;
                NotifyPropertyChanged("IsDropDestination");
                FolderIsDropDestination = IsDropDestination == 1;
            }
        }

        public Contract_ImportFolder ToContract()
        {
            Contract_ImportFolder contract = new Contract_ImportFolder();
            contract.ImportFolderID = this.ImportFolderID;
            contract.ImportFolderType = this.ImportFolderType;

            // Make sure to format folder with trailing slash first
            contract.ImportFolderLocation = FormatImportFolderLocation(this.ImportFolderLocation);
            contract.ImportFolderName = this.ImportFolderName;
            contract.IsDropSource = this.IsDropSource;
            contract.IsDropDestination = this.IsDropDestination;
            contract.IsWatched = this.IsWatched;
            contract.CloudID = this.CloudID;
            return contract;
        }

        private string FormatImportFolderLocation(string importFolderLocation)
        {
            if (importFolderLocation.Length > 0 && importFolderLocation.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                while (importFolderLocation.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    importFolderLocation = importFolderLocation.Substring(0, importFolderLocation.Length - 1);
                }
            }

            importFolderLocation += Path.DirectorySeparatorChar;

            return importFolderLocation;
        }
    }
}