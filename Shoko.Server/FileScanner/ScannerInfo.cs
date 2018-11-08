using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Shoko.Commons.Extensions;
using Shoko.Commons.Notification;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.FileScanner
{

    public class ScannerInfo 
    {
        private SynchronizationContext _synchronizationContext;
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propname)
        {
            if (_synchronizationContext == null || SynchronizationContext.Current == _synchronizationContext)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
            else
                _synchronizationContext.Send(InternalProperyChanged, new PropertyChangedEventArgs(propname));
        }

        public ScannerInfo()
        {
            _synchronizationContext = SynchronizationContext.Current;
        }

        public void OnPropertyChanged(Expression<Func<object>> expr)
        {
            MemberExpression me = INotifyPropertyChangedExtensions.Resolve(expr);
            if (me != null)
            {
                NotifyPropertyChanged(me.Member.Name);
            }
        }
        private void InternalProperyChanged(object param)
        {
            PropertyChanged?.Invoke(this, (PropertyChangedEventArgs)param);
        }

        public void OnPropertyChanged(params Expression<Func<object>>[] props)
        {
            foreach (Expression<Func<object>> o in props)
            {
                OnPropertyChanged(o);
            }
        }

        public Scan Scan { get; }
        public int Count { get; set; }
        public bool IsPaused { get; set; }

        public bool CanBePaused => Scan.Status == (int) ScanStatus.Running && !IsPaused;
        public bool CanBeResumed => Scan.Status == (int)ScanStatus.Running && IsPaused;
        public bool CanBeCanceled => Scan.Status == (int) ScanStatus.Running;
        public bool CanBeStarted => Scan.Status != (int)ScanStatus.Running;

        public AsyncObservableCollection<ScanFile> ErrorFiles { get; set; } = new AsyncObservableCollection<ScanFile>();


        public string State { get; set; }
        public ScannerInfo(Scan s)
        {
            Scan = s;
        }

        public string TitleText
        {
            get
            {
                return Scan.CreationTime.ToString(CultureInfo.CurrentUICulture) + " (" + string.Join(" | ",
                           this.Scan.GetImportFolderList()
                               .Select(a => Repo.Instance.ImportFolder.GetByID(a))
                               .Where(a => a != null)
                               .Select(a => a.ImportFolderLocation
                                   .Split(
                                       new[]
                                       {
                                           Path.PathSeparator, Path.DirectorySeparatorChar,
                                           Path.AltDirectorySeparatorChar
                                       }, StringSplitOptions.RemoveEmptyEntries)
                                   .LastOrDefault())
                               .ToArray()) + ")";
            }
        }


    }
}