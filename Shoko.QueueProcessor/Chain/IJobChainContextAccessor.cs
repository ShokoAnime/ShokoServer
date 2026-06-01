using System;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Chain;

public interface IJobChainContextAccessor
{
    JobChainContext? GetCurrentContext();

    // Read results — type-based returns the most recent result of that type in the chain
    T? GetResult<T>(Type jobType);
    T? GetResult<TJob, T>() where TJob : IQueueJob;
    // Precise lookup when the same job type appears more than once in the chain
    T? GetResult<T>(Guid jobId);

    T? GetData<T>(string key);

    // Set result for the currently-executing job (called automatically by BaseJob<T>)
    void SetResult<T>(Type jobType, T value);
    void SetData<T>(string key, T? value);
}
