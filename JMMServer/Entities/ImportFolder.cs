using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using JMMContracts;
using JMMServer.Repositories;
using NutzCode.CloudFileSystem;
using NutzCode.CloudFileSystem.OAuth.Windows.WPF;
using NutzCode.CloudFileSystem.Plugins.GoogleDrive;

namespace JMMServer.Entities
{
    public class ImportFolder : INotifyPropertyChanged
    {
        public int ImportFolderID { get; private set; }
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
                    if (CloudID.HasValue)
                    {
                        CloudAccount cl = new CloudAccountRepository().GetByID(CloudID.Value);
                        if (cl == null)
                            throw new Exception("Cloud Account Not Found");
                        _filesystem = cl.FileSystem;                        
                    }
                    else
                    {

                       
                        FileSystemResult<IFileSystem> ff= CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == "Local File System")?.Init(ImportFolderName, null, null);
                        if (ff==null || !ff.IsOk)
                            throw new Exception(ff?.Error ?? "Error Opening Local Filesystem");
                        _filesystem = ff.Result;
                    }
                }
                return _filesystem;
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
                    return new CloudAccountRepository().GetByID(CloudID.Value);
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
            contract.ImportFolderLocation = this.ImportFolderLocation;
            contract.ImportFolderName = this.ImportFolderName;
            contract.IsDropSource = this.IsDropSource;
            contract.IsDropDestination = this.IsDropDestination;
            contract.IsWatched = this.IsWatched;
            contract.CloudID = this.CloudID;
            return contract;
        }
    }
}