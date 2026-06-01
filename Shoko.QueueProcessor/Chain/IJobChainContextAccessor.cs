using System;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Chain;

public interface IJobChainContextAccessor
{
    JobChainContext? GetCurrentContext();

    T? GetResult<T>(Type jobType);
    T? GetResult<TJob, T>() where TJob : IQueueJob;
    T? GetData<T>(string key);

    void SetResult<T>(Type jobType, T value);
    void SetData<T>(string key, T? value);
}
