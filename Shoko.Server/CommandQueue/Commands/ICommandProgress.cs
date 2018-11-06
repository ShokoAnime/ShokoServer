namespace Shoko.Server.CommandQueue.Commands
{
    public interface ICommandProgress
    {
        ICommand Command { get; set; }
        double Progress { get; set; }
        CommandResultStatus Status { get; set; }
        string Error { get; set; }
    }
}