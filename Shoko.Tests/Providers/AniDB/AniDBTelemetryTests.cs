using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Settings;
using Shoko.Server.Telemetry;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// The source and meter are static, so each test tells its own telemetry apart by a command name of its own, or by the
/// thread a ban is counted on.
/// </summary>
public class AniDBTelemetryTests
{
    private const string Password = "hunter2";

    private sealed class Recorder : IDisposable
    {
        private readonly ActivityListener _spanListener;

        private readonly MeterListener _meterListener = new();

        public ConcurrentQueue<Activity> Spans { get; } = new();

        public ConcurrentQueue<(string Instrument, double Value, Dictionary<string, object?> Tags)> Measurements { get; } = new();

        public Recorder()
        {
            _spanListener = new()
            {
                ShouldListenTo = source => source.Name == AniDBTelemetry.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Spans.Enqueue,
            };
            ActivitySource.AddActivityListener(_spanListener);

            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AniDBTelemetry.Name)
                    listener.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Measurements.Enqueue((instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)))
            );
            _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Measurements.Enqueue((instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)))
            );
            _meterListener.Start();
        }

        public void Dispose()
        {
            _spanListener.Dispose();
            _meterListener.Dispose();
        }
    }

    private static async Task<(AniDBUDPConnectionHandler Handler, AniDBTestDoubles.StubSocketHandler Socket)> CreateUdpHandler()
    {
        var socket = new AniDBTestDoubles.StubSocketHandler { IsConnected = true };
        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(p => p.GetSettings(It.IsAny<bool>())).Returns(new ServerSettings());
        var connectivity = new Mock<IConnectivityService>();
        connectivity.SetupGet(c => c.NetworkAvailability).Returns(NetworkAvailability.Internet);

        var handler = new AniDBUDPConnectionHandler(
            new Mock<IRequestFactory>().Object,
            NullLoggerFactory.Instance,
            settingsProvider.Object,
            AniDBTestDoubles.UdpRateLimiter(),
            connectivity.Object,
            new AniDbBanStateService(NullLogger<AniDbBanState>.Instance),
            AniDBTestDoubles.SocketHandlerFactory(socket)
        );
        await handler.InitAsync("tester", "secret", "anidb.invalid", 9000, 4556, TestContext.Current.CancellationToken);
        return (handler, socket);
    }

    private static string NewCommand()
        => "TESTCMD" + string.Concat(Guid.NewGuid().ToString("N").Where(char.IsAsciiLetter)).ToUpperInvariant();

    [Fact]
    public async Task AUdpRequest_ReportsOnlyTheNameOfItsCommand()
    {
        using var recorder = new Recorder();
        var (handler, socket) = await CreateUdpHandler();
        socket.Respond(Encoding.ASCII.GetBytes("300 PONG"));
        var command = NewCommand();

        await handler.SendDirectlyAsync($"{command} user=tester&pass={Password}&s=abcde", cancellationToken: TestContext.Current.CancellationToken);

        var span = Assert.Single(recorder.Spans, span => span.OperationName == $"anidb.udp {command}");
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal(command, span.GetTagItem("shoko.anidb.command"));
        Assert.Equal("udp", span.GetTagItem("shoko.anidb.transport"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.DoesNotContain(span.Tags, tag => tag.Value?.Contains(Password) is true);

        Assert.Contains(recorder.Measurements, measurement => measurement.Instrument == "shoko.anidb.request.duration" && Equals(measurement.Tags.GetValueOrDefault("shoko.anidb.command"), command));
        Assert.Contains(recorder.Measurements, measurement => measurement.Instrument == "shoko.anidb.rate_limit.wait.duration" && Equals(measurement.Tags["shoko.anidb.transport"], "udp"));
    }

    [Fact]
    public async Task AnEmptyUdpReply_ReportsTheBanAsAFailure()
    {
        using var recorder = new Recorder();
        var (handler, socket) = await CreateUdpHandler();
        socket.Respond(new byte[8]);
        var command = NewCommand();

        await Assert.ThrowsAsync<AniDBBannedException>(() => handler.SendDirectlyAsync(command, cancellationToken: TestContext.Current.CancellationToken));

        var span = Assert.Single(recorder.Spans, span => span.OperationName == $"anidb.udp {command}");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(typeof(AniDBBannedException).FullName, span.GetTagItem("error.type"));
        Assert.Contains(recorder.Measurements, measurement => measurement.Instrument == "shoko.anidb.request.duration"
            && Equals(measurement.Tags.GetValueOrDefault("shoko.anidb.command"), command)
            && Equals(measurement.Tags.GetValueOrDefault("error.type"), typeof(AniDBBannedException).FullName));
    }

    [Fact]
    public void ABan_IsCountedOncePerBan()
    {
        var bans = new List<object?>();
        var thread = Environment.CurrentManagedThreadId;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == AniDBTelemetry.Name && instrument.Name == "shoko.anidb.bans")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (Environment.CurrentManagedThreadId == thread)
                bans.Add(tags.ToArray().Single(tag => tag.Key == "shoko.anidb.transport").Value);
        });
        listener.Start();
        var state = new AniDbBanState(AnidbBanType.HTTP, 1, NullLogger<AniDbBanState>.Instance);

        state.Ban();
        state.Ban();

        Assert.Equal(["http"], bans);
    }

    [Theory]
    [InlineData("AUTH user=tester&pass=hunter2", "AUTH")]
    [InlineData("PING", "PING")]
    [InlineData("ping nat=1", "PING")]
    [InlineData("FILE\nfid=1", "FILE")]
    [InlineData("", "UNKNOWN")]
    [InlineData(" AUTH", "UNKNOWN")]
    public void TheCommandsName_IsOnlyTheLettersItStartsWith(string command, string expected)
        => Assert.Equal(expected, AniDBTelemetry.GetCommandName(command));
}
