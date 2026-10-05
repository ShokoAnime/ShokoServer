using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Scheduling.Watchdog;
using Shoko.Server.Services;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the <see cref="AiringScheduleService"/>.
/// <br/>
/// These are separate from the <see cref="ServerSettings"/> to prevent
/// clients from modifying them through the settings endpoint.
/// </summary>
public class AiringScheduleServiceSettings : INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// The smallest retention window the service accepts. Anything below it is
    /// clamped on load, since a shorter window would remove a run's history
    /// before the run itself is over.
    /// </summary>
    public const int MinimumRetentionMonths = 3;

    /// <summary>
    /// The largest retention window the service accepts, a century. Anything
    /// above it is clamped on load. Turn <see cref="AutoCleanup"/> off to keep
    /// everything.
    /// </summary>
    public const int MaximumRetentionMonths = 1200;

    /// <summary>
    /// The shortest sweep interval the service accepts. A sweep walks a whole
    /// source rather than polling it, and anything below this is a provider, or
    /// a user, trying to use the sweep driver as a timer.
    /// </summary>
    public static readonly TimeSpan MinimumSweepInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The sweep interval used for a provider that suggests none. Providers
    /// that know better say so through
    /// <see cref="ISweepingAiringScheduleProvider.SuggestedSweepInterval"/>.
    /// </summary>
    public static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// The smallest chunk deadline the service accepts. Below this the
    /// per-chunk overhead is most of the chunk.
    /// </summary>
    public const int MinimumSweepBudgetSeconds = 1;

    /// <summary>
    /// The largest chunk deadline the service accepts. A rate limited source gets more done per
    /// chunk the longer a chunk is, and the per-chunk overhead is paid once either way, so the
    /// ceiling is generous. What it costs is a worker held for that long, and a chunk that overruns
    /// it reported by the queue watchdog that much later.
    /// </summary>
    public const int MaximumSweepBudgetSeconds = 600;

    /// <summary>
    /// A list of provider ids in order of priority. Priority ranks sources, not
    /// where someone would rather watch; that is what
    /// <see cref="PreferredChannels"/> and <see cref="PreferredTracks"/> are for.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public List<Guid> Priority { get; set; } = [];

    /// <summary>
    /// The enabled kinds of each provider by id. A provider with no enabled
    /// kinds is disabled, and kinds a provider no longer declares are trimmed
    /// on load.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<Guid, List<AiringKind>> EnabledKinds { get; set; } = [];

    /// <summary>
    /// How long after a sweep finishes before the next one starts, per provider
    /// by id. Seeded from each provider's own suggestion on first sight, owned
    /// by the user afterwards, and clamped to
    /// <see cref="MinimumSweepInterval"/> on load.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<Guid, TimeSpan> SweepIntervals { get; set; } = [];

    /// <summary>
    /// How long one chunk of a sweep may run before the provider's token is
    /// cancelled, in seconds. Clamped to between
    /// <see cref="MinimumSweepBudgetSeconds"/> and
    /// <see cref="MaximumSweepBudgetSeconds"/> on use.
    /// </summary>
    /// <remarks>
    /// A chunk holds a queue worker for as long as it runs, and a worker is
    /// shared, so this is the server's to set and never the provider's. The
    /// sweep job runs at most four at a time, so four chunks at the full budget
    /// is the most this can hold. The queue watchdog is told the budget through
    /// <see cref="AiringScheduleSweepWatchdogThreshold"/> and watches the job
    /// against a threshold above it, so a chunk that runs its budget out is not
    /// mistaken for a stuck worker while one that overruns it is still reported.
    /// </remarks>
    [Range(MinimumSweepBudgetSeconds, MaximumSweepBudgetSeconds)]
    public int SweepBudgetSeconds { get; set; } = 60;

    /// <summary>
    /// The server's default channel preference, used by every read that doesn't
    /// bring its own. Empty means no preference at all.
    /// </summary>
    public List<Guid> PreferredChannels { get; set; } = [];

    /// <summary>
    /// The channels every airing read leaves out unless it asks for them or
    /// names one of them. Empty means no channel is hidden.
    /// </summary>
    public List<Guid> HiddenChannels { get; set; } = [];

    /// <summary>
    /// The server's default track preference, used by every read that doesn't
    /// bring its own. Empty means no preference at all.
    /// </summary>
    public List<AiringTrackPreference> PreferredTracks { get; set; } = [new(AiringKind.Original)];

    /// <summary>
    /// Whether to remove schedules and their airings once a run has been over
    /// for <see cref="RetentionMonths"/>.
    /// </summary>
    public bool AutoCleanup { get; set; } = true;

    /// <summary>
    /// How long after a run ends its schedules and airings are kept, in months.
    /// Values outside <see cref="MinimumRetentionMonths"/> and
    /// <see cref="MaximumRetentionMonths"/> are clamped on load.
    /// </summary>
    [Range(MinimumRetentionMonths, MaximumRetentionMonths)]
    public int RetentionMonths { get; set; } = 12;

    private List<MetadataSource> _seasonDetailSourceOrder = [MetadataSource.TMDB];

    /// <summary>
    /// The sources whose studios and genres the season view uses, highest
    /// ranked first. A source not listed is not used, except AniDB, which is
    /// always used: at its place when listed, else first. Duplicates are
    /// dropped.
    /// </summary>
    public List<MetadataSource> SeasonDetailSourceOrder
    {
        get => _seasonDetailSourceOrder;
        set => _seasonDetailSourceOrder = value.Distinct().ToList();
    }
}
