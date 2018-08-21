using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace Shoko.Server.Workers
{
    public static class WorkerExtensions
    {
        public static async Task<WorkResult<T>> RunAsync<T>(this IWorkCommand<T> command, string workblob, IProgress<IWorkProgress<T>> progress = null, CancellationToken token = default(CancellationToken)) where T : IWorkUnit
        {
            T workunit = await command.DeserializeAsync(workblob);
            if (workunit != null)
                return await command.RunAsync(workunit, progress, token);
            return new WorkResult<T>(WorkResultStatus.Error,"Unable to deserialize workblob");
        }

        public static WorkResult<T> Run<T>(this IWorkCommand<T> command, T workunit, IProgress<IWorkProgress<T>> progress = null, CancellationToken token = default(CancellationToken)) where T : IWorkUnit
        {
            return Task.Run(async () => await command.RunAsync(workunit, progress, token), token).Result;
        }
        public static WorkResult<T> Run<T>(this IWorkCommand<T> command, string workblob, IProgress<IWorkProgress<T>> progress = null, CancellationToken token = default(CancellationToken)) where T : IWorkUnit
        {
            return Task.Run(async () => await command.RunAsync(workblob, progress, token), token).Result;
        }
        public static T Deserialize<T>(this IWorkCommand<T> command, string str) where T : IWorkUnit
        {
            return Task.Run(async () => await command.DeserializeAsync(str)).Result;
        }
        public static string Serialize<T>(this IWorkCommand<T> command, T workunit) where T : IWorkUnit
        {
            return Task.Run(async () => await command.SerializeAsync(workunit)).Result;
        }

        public static string GenericSerialize<T>(this IWorkCommand<T> command, T item) where T : IWorkUnit
        {
            return JsonConvert.SerializeObject(item);
        }
        public static T GenericDeserialize<T>(this IWorkCommand<T> command, string str) where T : IWorkUnit
        {
            if (string.IsNullOrEmpty(str))
                return default(T);
            return JsonConvert.DeserializeObject<T>(str);
        }

    }
}
