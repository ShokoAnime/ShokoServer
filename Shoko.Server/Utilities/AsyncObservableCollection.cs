using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

namespace Shoko.Server.Utilities
{
    public class AsyncObservableCollection<T> : ObservableCollection<T>
    {
        private readonly SynchronizationContext _synchronizationContext;

        public AsyncObservableCollection()
        {
            _synchronizationContext = SynchronizationContext.Current;
        }

        public AsyncObservableCollection(IEnumerable<T> list) : base(list)
        {
            _synchronizationContext = SynchronizationContext.Current;
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_synchronizationContext == null || SynchronizationContext.Current == _synchronizationContext)
                RaiseCollectionChanged(e);
            else
                _synchronizationContext.Send(RaiseCollectionChanged, e);
        }

        private void RaiseCollectionChanged(object param)
        {
            base.OnCollectionChanged((NotifyCollectionChangedEventArgs) param);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_synchronizationContext == null || SynchronizationContext.Current == _synchronizationContext)
                RaisePropertyChanged(e);
            else
                _synchronizationContext.Send(RaisePropertyChanged, e);
        }

        private void RaisePropertyChanged(object param)
        {
            base.OnPropertyChanged((PropertyChangedEventArgs) param);
        }
    }
}