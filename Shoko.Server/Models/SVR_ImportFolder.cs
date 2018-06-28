using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Serialization;
using Nancy.Json;
using Newtonsoft.Json;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Notification;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_ImportFolder : ImportFolder, INotifyPropertyChangedExt
    {
        //private static Logger logger = LogManager.GetCurrentClassLogger();

        private byte[] _bitmap;


        private IFileSystem _filesystem;


        public new int IsWatched
        {
            get { return base.IsWatched; }
            set { this.SetField(() => base.IsWatched, (r) => base.IsWatched = r, value, () => IsWatched, () => FolderIsWatched); }
        }


        public new int IsDropSource
        {
            get { return base.IsDropSource; }
            set { this.SetField(() => base.IsDropSource, (r) => base.IsDropSource = r, value, () => IsDropSource, () => FolderIsDropSource); }
        }


        public new int IsDropDestination
        {
            get { return base.IsDropDestination; }
            set { this.SetField(() => base.IsDropDestination, (r) => base.IsDropDestination = r, value, () => IsDropDestination, () => FolderIsDropDestination); }
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

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        [NotMapped]
        public IFileSystem FileSystem
        {
            get
            {
                if (_filesystem == null)
                {
                    if (CloudID != null)
                    {
                        SVR_CloudAccount cl = Repo.CloudAccount.GetByID(CloudID.Value);
                        if (cl == null)
                            throw new Exception("Cloud Account Not Found");
                        _filesystem = cl.FileSystem;
                    }
                    else
                    {
                        IFileSystem ff = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == "Local File System")?.Init("", null);
                        if (ff==null || ff.Status != Status.Ok)
                            throw new Exception(ff?.Error ?? "Error Opening Local Filesystem");
                        _filesystem = ff;
                    }
                }

                return _filesystem;
            }
        }

        private byte[] _bitmap;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public byte[] Bitmap
        {
            get
            {
                if (_bitmap != null)
                {
                    return _bitmap;
                }

                _bitmap = CloudID.HasValue ? CloudAccount.Bitmap : SVR_CloudAccount.CreateLocalFileSystemAccount().Bitmap;

                return _bitmap;
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        [NotMapped]
        public IDirectory BaseDirectory
        {
            get
            {
                IObject fr = FileSystem.Resolve(ImportFolderLocation);

                if (fr != null && fr.IsOk && fr.Result is IDirectory)
                    return (IDirectory) fr.Result;
                throw new Exception("Import Folder not found '" + ImportFolderLocation + "'");
            }
        }

        [NotMapped]
        public SVR_CloudAccount CloudAccount => CloudID.HasValue ? Repo.CloudAccount.GetByID(CloudID.Value) : null;


        [NotMapped]
        public string CloudAccountName => CloudID.HasValue ? CloudAccount.Name : "Local FileSystem";

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        [NotMapped]
        public bool FolderIsWatched => IsWatched == 1;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        [NotMapped]
        public bool FolderIsDropSource => IsDropSource == 1;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        [NotMapped]
        public bool FolderIsDropDestination => IsDropDestination == 1;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }

        public override string ToString() => $"{ImportFolderName} - {ImportFolderLocation} ({ImportFolderID})";
    }
}