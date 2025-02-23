using System.ComponentModel;

namespace Shoko.Commons.Notification
{
    public interface INotifyPropertyChangedExt : INotifyPropertyChanged
    {
        void NotifyPropertyChanged(string propname);
    }
}