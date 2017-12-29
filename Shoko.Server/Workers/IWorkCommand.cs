using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Workers
{
    public interface IWorkCommand<T> where T : IWorkUnit
    {
        string Name { get; }
        Task<WorkResult<T>> RunAsync(T workunit, IProgress<IWorkProgress<T>> progress=null,CancellationToken token=default(CancellationToken));
        Task<T> DeserializeAsync(string str);
        Task<string> SerializeAsync(T workunit);

    }
}