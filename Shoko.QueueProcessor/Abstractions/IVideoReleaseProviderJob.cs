using Shoko.Abstractions.Video.Release;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Links a concrete job class to a specific <see cref="IReleaseInfoProvider"/> type, making the
/// job the designated handler for that provider in the import pipeline. The chain builder is
/// completely agnostic of this interface — it accepts any <see cref="IQueueJob"/>.
/// <para>
/// Implements <see cref="IQueueJob"/> so the job can be used anywhere a job is expected without
/// any extra plumbing.
/// </para>
/// <para>
/// Both <see cref="IReleaseInfoProvider"/> and <see cref="IReleaseInfoProvider{TConfiguration}"/>
/// are valid constraints for <typeparamref name="TProvider"/>.
/// </para>
/// </summary>
/// <typeparam name="TProvider">The <see cref="IReleaseInfoProvider"/> type this job handles.</typeparam>
public interface IVideoReleaseProviderJob<TProvider> : IQueueJob
    where TProvider : IReleaseInfoProvider { }
