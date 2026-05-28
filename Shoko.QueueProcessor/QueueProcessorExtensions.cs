#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
        var jobTypes = new List<Type>();
        var assemblies = scanAssemblies.Prepend(Assembly.GetCallingAssembly()).Distinct();
        foreach (var asm in assemblies)
        {
            foreach (var type in asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IQueueJob).IsAssignableFrom(t)))
            {
                services.TryAddTransient(type);
                jobTypes.Add(type);
            }
        }

        var jobTypeRegistry = new QueueJobTypeRegistry(jobTypes);
        services.AddSingleton(jobTypeRegistry);

        // ── Orchestration ────────────────────────────────────────────────────
        services.AddSingleton(_ => ConcurrencyRegistry.Build(jobTypeRegistry.JobTypes.Distinct(), options.LimitedConcurrencyOverrides, options.MaxTotalWorkers));

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
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PersistenceBuffer>>(),
            options.FlushIntervalMs,
            options.MaxFlushBatch));

        services.AddSingleton(sp => new QueueOrchestrator(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<QueueOrchestrator>>(),
            sp.GetRequiredService<PersistenceBuffer>(),
            sp.GetRequiredService<IJobRepository>(),
            sp.GetRequiredService<ConcurrencyRegistry>(),
            sp.GetRequiredService<RetryPolicyResolver>(),
            sp.GetRequiredService<QueueMetrics>(),
            options.MaxTotalWorkers));

        services.AddSingleton(sp => new PoolDiscovery(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PoolDiscovery>>(),
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
    public static async System.Threading.Tasks.Task MigrateQueueDatabaseAsync(
        this IServiceProvider serviceProvider,
        System.Threading.CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();
        await db.Database.MigrateAsync(ct);
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
