using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;

namespace Shoko.QueueProcessor;

/// <summary>
/// Extension methods for wiring the queue processor into an <see cref="IServiceCollection"/>.
/// </summary>
public static class QueueProcessorExtensions
{
    /// <summary>
    /// Registers all queue processor services. Call this from the host's
    /// <c>ConfigureServices</c> / <c>AddServices</c> method.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">Options configuration delegate.</param>
    /// <param name="scanAssemblies">
    /// Additional assemblies to scan for <see cref="IQueueJob"/> implementations.
    /// The calling assembly is always scanned automatically.
    /// </param>
    public static IServiceCollection AddQueueProcessor(
        this IServiceCollection services,
        Action<QueueProcessorOptions> configure,
        params Assembly[] scanAssemblies)
    {
        var options = new QueueProcessorOptions();
        configure(options);
        services.AddSingleton(options);

        // ── Storage ──────────────────────────────────────────────────────────
        RegisterDbContext(services, options);
        services.AddScoped<IJobRepository, JobRepository>();

        // ── Job type registration ─────────────────────────────────────────────
        // Jobs are resolved from DI by their concrete type only — never via IQueueJob.
        // The interface is used solely for reflection-based discovery (attributes, TypeName,
        // etc.), not for DI service lookup. Registering them as IQueueJob would allow
        // GetServices<IQueueJob>() to instantiate every job at singleton-build time,
        // causing a circular dependency with ConcurrencyRegistry.
        //
        // The registry is a freeze-on-first-read builder: plugins can append additional
        // job types via AddQueueJobsFromAssembly while service registration is still open;
        // the first downstream read (from the worker pool hosted service) freezes it.
        var jobTypeRegistry = new QueueJobTypeRegistry();
        services.AddSingleton(jobTypeRegistry);

        var assemblies = scanAssemblies.Prepend(Assembly.GetCallingAssembly()).Distinct();
        foreach (var asm in assemblies)
            ScanAssemblyForJobs(services, jobTypeRegistry, asm);

        // ── Orchestration ────────────────────────────────────────────────────
        services.AddSingleton(sp => ConcurrencyRegistry.Build(
            sp.GetRequiredService<QueueJobTypeRegistry>().JobTypes.Distinct(),
            options.LimitedConcurrencyOverrides,
            options.MaxTotalWorkers));

        services.AddSingleton(new RetryPolicy
        {
            MaxRetries = options.RetryMaxAttempts,
            BaseDelay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds),
            MaxDelay = TimeSpan.FromSeconds(options.RetryMaxDelaySeconds)
        });
        services.AddSingleton<RetryPolicyResolver>();

        services.AddSingleton(new QueueMetrics(options.MetricsWindowSeconds, options.MetricsRollingAvgSamples));

        services.AddSingleton(sp => new PersistenceBuffer(
            sp.GetRequiredService<IJobRepository>(),
            sp.GetRequiredService<ILogger<PersistenceBuffer>>(),
            options.FlushIntervalMs,
            options.MaxFlushBatch));

        services.AddSingleton(sp => new QueueOrchestrator(
            sp.GetRequiredService<ILogger<QueueOrchestrator>>(),
            sp.GetRequiredService<PersistenceBuffer>(),
            sp.GetRequiredService<IJobRepository>(),
            sp.GetRequiredService<ConcurrencyRegistry>(),
            sp.GetRequiredService<RetryPolicyResolver>(),
            sp.GetRequiredService<QueueMetrics>(),
            options.MaxTotalWorkers));

        services.AddSingleton(sp => new PoolDiscovery(
            sp.GetRequiredService<ILogger<PoolDiscovery>>(),
            options.MaxTotalWorkers,
            options.DefaultPoolMaxWorkers,
            options.LimitedConcurrencyOverrides));

        // ── Events ───────────────────────────────────────────────────────────
        services.AddSingleton<QueueStateEventHandler>();

        // ── Scheduler façade ─────────────────────────────────────────────────
        services.AddSingleton<IQueueScheduler, QueueScheduler>();
        services.AddSingleton<QueueHandler>();

        // ── Hosted services ──────────────────────────────────────────────────
        services.AddSingleton<WorkerPoolManager>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<WorkerPoolManager>());
        services.AddSingleton<RecurringJobRegistry>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RecurringJobRegistry>());

        return services;
    }

    /// <summary>
    /// Applies any pending EF Core migrations to the queue database, creating it if it does not exist.
    /// Call this from <c>IHost.StartAsync</c> or at the start of <c>WorkerPoolManager.StartAsync</c>.
    /// </summary>
    public static async Task MigrateQueueDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Scans <paramref name="assembly"/> for concrete <see cref="IQueueJob"/> implementations,
    /// registers each as a transient service, and appends them to the shared
    /// <see cref="QueueJobTypeRegistry"/>. Call from a plugin's
    /// <c>IPluginServiceRegistration.RegisterServices</c> to make plugin-defined jobs visible
    /// to the worker pool, concurrency registry, and recurring job registry.
    /// </summary>
    /// <remarks>
    /// Must be called before the worker pool starts (i.e. during service registration). Calling
    /// after the registry has frozen throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    public static IServiceCollection AddQueueJobsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var registry = services
            .Where(d => d.ServiceType == typeof(QueueJobTypeRegistry))
            .Select(d => d.ImplementationInstance as QueueJobTypeRegistry)
            .FirstOrDefault(r => r is not null)
            ?? throw new InvalidOperationException(
                $"{nameof(AddQueueJobsFromAssembly)} requires {nameof(AddQueueProcessor)} to have been called first.");
        ScanAssemblyForJobs(services, registry, assembly);
        return services;
    }

    private static void ScanAssemblyForJobs(IServiceCollection services, QueueJobTypeRegistry registry, Assembly assembly)
    {
        var jobTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IQueueJob).IsAssignableFrom(t))
            .ToArray();
        foreach (var type in jobTypes)
            services.TryAddTransient(type);
        registry.Add(jobTypes);
    }

    private static void RegisterDbContext(IServiceCollection services, QueueProcessorOptions options)
    {
        switch (options.Provider)
        {
            case DatabaseProvider.SQLite:
                services.AddScoped<QueueDbContext>(_ => new SqliteQueueDbContext(options.ConnectionString));
                break;
            case DatabaseProvider.MySQL:
                services.AddScoped<QueueDbContext>(_ => new MySqlQueueDbContext(options.ConnectionString));
                break;
            case DatabaseProvider.SqlServer:
                services.AddScoped<QueueDbContext>(_ => new SqlServerQueueDbContext(options.ConnectionString));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Provider));
        }
    }
}
