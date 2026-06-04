using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Chain;

public interface IJobChainContextAccessor
{
    JobChainContext? GetCurrentContext();

    /// <summary>Returns the most recent result stored by any job with <paramref name="jobKey"/>.</summary>
    T? GetResult<T>(string jobKey);

    /// <summary>
    /// Returns all results stored by the job with <paramref name="jobId"/>, in chronological order.
    /// Index 0 is the first attempt; the last index is the most recent attempt.
    /// </summary>
    IReadOnlyList<T> GetResult<T>(Guid jobId);

    T? GetData<T>(string key);

    /// <summary>Stores a result for the currently-executing job (called by job implementations).</summary>
    void SetResult<T>(T value);
    void SetData<T>(string key, T? value);
}
