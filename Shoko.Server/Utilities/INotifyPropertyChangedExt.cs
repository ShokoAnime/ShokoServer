using System.ComponentModel;

namespace Shoko.Server.Utilities
{
    public interface INotifyPropertyChangedExt : INotifyPropertyChanged
    {
        void NotifyPropertyChanged(string propname);
    }
}
