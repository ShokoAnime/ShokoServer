using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NLog;
using Shoko.Commons.Notification;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_ImportFolder : ImportFolder, INotifyPropertyChangedExt, IImportFolder
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public new int IsWatched
        {
            get { return base.IsWatched; }
            set
            {
                this.SetField(() => base.IsWatched, r => base.IsWatched = r, value, () => IsWatched,
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
                this.SetField(() => base.IsDropSource, r => base.IsDropSource = r, value, () => IsDropSource,
                    () => FolderIsDropSource);
            }
        }


        public new int IsDropDestination
        {
            get { return base.IsDropDestination; }
            set
            {
                this.SetField(() => base.IsDropDestination, r => base.IsDropDestination = r, value,
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

        [JsonIgnore]
        [XmlIgnore]
        public DirectoryInfo BaseDirectory
        {
            get
            {
                if (!Directory.Exists(ImportFolderLocation))
                    throw new Exception("Import Folder not found '" + ImportFolderLocation + "'");
                return new DirectoryInfo(ImportFolderLocation);
            }
        }

        [JsonIgnore]
        [XmlIgnore]
        public bool FolderIsWatched => IsWatched == 1;

        [JsonIgnore]
        [XmlIgnore]
        public bool FolderIsDropSource => IsDropSource == 1;

        [JsonIgnore]
        [XmlIgnore]
        public bool FolderIsDropDestination => IsDropDestination == 1;

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2})", ImportFolderName, ImportFolderLocation, ImportFolderID);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }

        string IImportFolder.Location => ImportFolderLocation;

        DropFolderType IImportFolder.DropFolderType
        {
            get
            {
                if (IsDropSource == 1 && IsDropDestination == 1) return DropFolderType.Destination | DropFolderType.Source;
                if (IsDropSource != 1 && IsDropDestination != 1) return DropFolderType.Excluded;
                if (IsDropDestination == 1) return DropFolderType.Destination;
                return DropFolderType.Source;
            }
        }

        string IImportFolder.Name => ImportFolderName;
    }
}