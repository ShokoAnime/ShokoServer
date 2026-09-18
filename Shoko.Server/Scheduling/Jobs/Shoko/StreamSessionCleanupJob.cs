using System;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[JobKeyMember("StreamSessionCleanup")]
[JobKeyGroup(JobKeyGroup.System)]
[DisallowConcurrentExecution]
public class StreamSessionCleanupJob(VideoStreamSessionManager sessionManager, ConfigurationProvider<VideoStreamPipelineSettings> configurationProvider) : BaseJob
{
    public override string TypeName => "Stream Session Cleanup";

    public override string Title => "Cleaning up idle video stream sessions";

    public override Task Execute()
    {
        var idleTimeout = TimeSpan.FromMinutes(configurationProvider.Load().SessionIdleTimeoutMinutes);
        sessionManager.EvictExpiredSessions(idleTimeout);
        return Task.CompletedTask;
    }
}
