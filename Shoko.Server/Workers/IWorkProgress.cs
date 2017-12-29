namespace Shoko.Server.Workers
{
    public interface IWorkProgress<T> where T: IWorkUnit
    {
        T Unit { get; }
        IWorkCommand<T> Command { get; }
        double Progress { get; }
    }

    public class BasicWorkProgress<T> : IWorkProgress<T> where T : IWorkUnit
    {
        public T Unit { get; set; }
        public IWorkCommand<T> Command { get; set; }
        public double Progress { get; set; }
    }
}