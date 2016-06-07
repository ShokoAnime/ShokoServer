using System.ComponentModel;
using JMMContracts;

namespace JMMServer.Entities
{
    public class ImportFolder : INotifyPropertyChanged
    {
        private bool folderIsDropDestination = true;

        private bool folderIsDropSource = true;

        private bool folderIsWatched = true;

        private int isDropDestination;

        private int isDropSource;

        private int isWatched;

        public ImportFolder()
        {
            FolderIsDropSource = IsDropSource == 1;
            FolderIsDropDestination = IsDropDestination == 1;
        }

        public int ImportFolderID { get; private set; }
        public int ImportFolderType { get; set; }
        public string ImportFolderName { get; set; }
        public string ImportFolderLocation { get; set; }

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

        public bool FolderIsWatched
        {
            get { return folderIsWatched; }
            set
            {
                folderIsWatched = value;
                NotifyPropertyChanged("FolderIsWatched");
            }
        }

        public bool FolderIsDropSource
        {
            get { return folderIsDropSource; }
            set
            {
                folderIsDropSource = value;
                NotifyPropertyChanged("FolderIsDropSource");
            }
        }

        public bool FolderIsDropDestination
        {
            get { return folderIsDropDestination; }
            set
            {
                folderIsDropDestination = value;
                NotifyPropertyChanged("FolderIsDropDestination");
            }
        }

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

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2})", ImportFolderName, ImportFolderLocation, ImportFolderID);
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var args = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, args);
            }
        }

        public Contract_ImportFolder ToContract()
        {
            var contract = new Contract_ImportFolder();
            contract.ImportFolderID = ImportFolderID;
            contract.ImportFolderType = ImportFolderType;
            contract.ImportFolderLocation = ImportFolderLocation;
            contract.ImportFolderName = ImportFolderName;
            contract.IsDropSource = IsDropSource;
            contract.IsDropDestination = IsDropDestination;
            contract.IsWatched = IsWatched;

            return contract;
        }
    }
}