using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Scoped service that gives jobs access to the current chain context.
/// One instance lives for the full chain lifetime (chain scope), so all jobs in a chain
/// share the same <see cref="JobChainContext"/> without any AsyncLocal machinery.
/// </summary>
public class JobChainContextAccessor : IJobChainContextAccessor
{
    private JobChainContext? _context;
    private Guid _currentJobId;
    private string _currentJobKey = string.Empty;

    public JobChainContext? GetCurrentContext() => _context;

    public T? GetResult<T>(string jobKey) => _context == null ? default : _context.GetResult<T>(jobKey);

    public IReadOnlyList<T> GetResult<T>(Guid jobId) => _context == null ? [] : _context.GetResult<T>(jobId);

    public T? GetData<T>(string key) => _context == null ? default : _context.GetData<T>(key);

    public void SetResult<T>(T value)
    {
        if (_context != null && _currentJobId != Guid.Empty)
            _context.SetResult(_currentJobId, _currentJobKey, value);
    }

    public void SetData<T>(string key, T? value) => _context?.SetData(key, value);

    // Called by Worker to hydrate on first job or after crash-recovery scope rebuild
    internal void Initialize(JobChainContext context) => _context = context;

    // Called by Worker before each job executes so SetResult tags results by job ID and key
    internal void SetCurrentJob(Guid jobId, string jobKey)
    {
        _currentJobId = jobId;
        _currentJobKey = jobKey;
    }
}
