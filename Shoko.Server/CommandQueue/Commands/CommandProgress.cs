namespace Shoko.Server.CommandQueue.Commands
{
    public class CommandProgress<T> : CommandProgress where T: ICommand
    {
        public new T Command
        {
            get => (T) base.Command;
            set => base.Command = value;
        }
    }

    public class CommandProgress : ICommandProgress
    {
        public ICommand Command { get; set; }
        public double Progress { get; set; }
        public CommandResultStatus Status { get; set; } = CommandResultStatus.Ok;
        public string Error { get; set; }
    }
}