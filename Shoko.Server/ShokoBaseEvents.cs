namespace Shoko.Server
{
    public class ShokoBaseEvents
    {
        public delegate bool MessageBoxHandler(string title, string message, bool iserror, bool yesno);

        public event MessageBoxHandler OnMessageBox;

        public bool DoMessageBox(string title, string message, bool iserror, bool yesno)
        {
            if (OnMessageBox != null)
                return OnMessageBox(title, message, iserror, yesno);
            return false;
        }
    }
}