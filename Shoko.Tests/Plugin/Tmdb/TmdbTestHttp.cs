using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Plugin.Tmdb;
using Shoko.Plugin.Tmdb.Api;
using TMDbLib.Client;
using TMDbLib.Utilities.Serializer;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   Answers TMDb's API from the fixtures, by the path asked for, so the
///   plugin's client reads them through TMDbLib as it would TMDb's answers.
/// </summary>
/// <remarks>
///   A path nobody routed answers 404 as TMDb does. A route may hold several
///   answers, given in turn, the last one again once the rest are used up.
/// </remarks>
internal sealed class TmdbRoutes : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Queue<Func<HttpResponseMessage>>> _routes = new(StringComparer.Ordinal);

    private readonly ConcurrentQueue<Uri> _requests = new();

    /// <summary>
    ///   The paths asked for, after <c>/3/</c>, in order.
    /// </summary>
    public IReadOnlyList<string> Paths => [.. _requests.Select(PathOf)];

    /// <summary>
    ///   The queries asked with, by path.
    /// </summary>
    public IReadOnlyList<Uri> Requests => [.. _requests];

    /// <summary>
    ///   Answers a path with a fixture file.
    /// </summary>
    /// <param name="path">The path, e.g. <c>tv/1001</c>.</param>
    /// <param name="fixture">The fixture's file name.</param>
    /// <returns>The routes.</returns>
    public TmdbRoutes Fixture(string path, string fixture)
        => Json(path, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Plugin", "Tmdb", "Fixtures", fixture)));

    /// <summary>
    ///   Answers a path with a body.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="json">The body.</param>
    /// <param name="status">The status code.</param>
    /// <param name="retryAfter">The <c>Retry-After</c> to send, if any.</param>
    /// <returns>The routes.</returns>
    public TmdbRoutes Json(string path, string json, HttpStatusCode status = HttpStatusCode.OK, TimeSpan? retryAfter = null)
    {
        _routes.GetOrAdd(path, _ => new()).Enqueue(() =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            if (retryAfter is { } wait)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
            return response;
        });
        return this;
    }

    /// <summary>
    ///   Answers a path with TMDb's error body.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="status">The status code.</param>
    /// <param name="retryAfter">The <c>Retry-After</c> to send, if any.</param>
    /// <returns>The routes.</returns>
    public TmdbRoutes Status(string path, HttpStatusCode status, TimeSpan? retryAfter = null)
        => Json(path, $$"""{"success":false,"status_code":{{(int)status}},"status_message":"{{status}}"}""", status, retryAfter);

    /// <summary>
    ///   How many times a path was asked for.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The count.</returns>
    public int Count(string path)
        => Paths.Count(asked => asked == path);

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request.RequestUri!);
        var path = PathOf(request.RequestUri!);
        if (!_routes.TryGetValue(path, out var answers) || answers.Count is 0)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"success":false,"status_code":34,"status_message":"The resource you requested could not be found."}""", Encoding.UTF8, "application/json"),
            });

        lock (answers)
            return Task.FromResult(answers.Count > 1 ? answers.Dequeue()() : answers.Peek()());
    }

    private static string PathOf(Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/');
        return path.StartsWith("3/", StringComparison.Ordinal) ? path[2..] : path;
    }
}

/// <summary>
///   Builds the plugin's TMDb client over <see cref="TmdbRoutes"/>, and
///   its configuration.
/// </summary>
internal static class TmdbTestClient
{
    // TMDbLib takes a message handler through a constructor it keeps for its own tests.
    private static readonly ConstructorInfo _clientConstructor = typeof(TMDbClient).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        [typeof(string), typeof(bool), typeof(string), typeof(ITMDbSerializer), typeof(IWebProxy), typeof(HttpMessageHandler)]
    ) ?? throw new InvalidOperationException("TMDbLib no longer takes a message handler.");

    /// <summary>
    ///   A client answering from the routes.
    /// </summary>
    /// <param name="routes">The routes.</param>
    /// <param name="configuration">The configuration; one with an API key when left out.</param>
    /// <param name="timeProvider">The clock; the system's when left out.</param>
    /// <returns>The client.</returns>
    public static TmdbApiClient Create(TmdbRoutes routes, TmdbConfiguration? configuration = null, TimeProvider? timeProvider = null)
        => new(
            Configuration(configuration ?? new() { UserApiKey = "test-key" }).Provider,
            NullLogger<TmdbApiClient>.Instance,
            apiKey => (TMDbClient)_clientConstructor.Invoke([apiKey, true, "api.themoviedb.org", null, null, routes]),
            timeProvider
        );

    /// <summary>
    ///   A configuration provider handing back one configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The provider, and the service to raise a save on.</returns>
    public static (ConfigurationProvider<TmdbConfiguration> Provider, Mock<IConfigurationService> Service) Configuration(TmdbConfiguration configuration)
    {
        var service = new Mock<IConfigurationService>();
        service.Setup(mock => mock.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(configuration);
        return (new(service.Object), service);
    }

    /// <summary>
    ///   Raises a save of the configuration, as the WebUI would.
    /// </summary>
    /// <param name="service">The configuration service.</param>
    public static void RaiseSaved(Mock<IConfigurationService> service)
        => service.Raise(mock => mock.Saved += null, new ConfigurationSavedEventArgs { ConfigurationInfo = null! });
}

/// <summary>
///   A clock the tests move by hand, whose timers fire once it passes their
///   due time.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _lock = new();

    private readonly List<ManualTimer> _timers = [];

    private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
            return _now;
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_lock)
            _timers.Add(timer);
        return timer;
    }

    /// <summary>
    ///   Moves the clock on, firing the timers it passes.
    /// </summary>
    /// <param name="time">How far.</param>
    public void Advance(TimeSpan time)
    {
        List<ManualTimer> due;
        lock (_lock)
        {
            _now += time;
            due = [.. _timers.Where(timer => timer.DueAt is { } at && at <= _now)];
            foreach (var timer in due)
                timer.DueAt = null;
        }

        foreach (var timer in due)
            timer.Fire();
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.GetUtcNow() + dueTime;
            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => DueAt = null;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
