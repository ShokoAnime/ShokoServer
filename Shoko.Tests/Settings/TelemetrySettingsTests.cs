using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Shoko.Server.Telemetry;
using Xunit;

namespace Shoko.Tests.Settings;

public sealed class TelemetrySettingsTests : IDisposable
{
    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-telemetry-settings-tests-{Guid.NewGuid():N}");

    public TelemetrySettingsTests()
        => Directory.CreateDirectory(_dataPath);

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    private static TelemetrySettings Valid()
        => new() { Endpoint = "http://localhost:4318", TracesEnabled = true };

    [Fact]
    public void ByDefault_NothingIsExportedAndNothingIsWarnedAbout()
    {
        var settings = new TelemetrySettings();

        Assert.Empty(settings.Endpoint);
        Assert.False(settings.TracesEnabled);
        Assert.False(settings.MetricsEnabled);
        Assert.False(settings.LogsEnabled);
        Assert.Null(TelemetryExportOptions.Create(settings, out var warnings));
        Assert.Empty(warnings);
    }

    [Fact]
    public void ASignalWithoutAnEndpoint_ExportsNothing()
    {
        Assert.Null(TelemetryExportOptions.Create(new() { TracesEnabled = true, MetricsEnabled = true, LogsEnabled = true }, out var warnings));
        Assert.Empty(warnings);
    }

    [Fact]
    public void AnEndpointWithEverySignalOff_ExportsNothingAndWarns()
    {
        Assert.Null(TelemetryExportOptions.Create(new() { Endpoint = "http://localhost:4318" }, out var warnings));
        Assert.Contains(warnings, warning => warning.Contains("all turned off"));
    }

    [Theory]
    [InlineData("http://localhost:4318", "http://localhost:4318/v1/traces")]
    [InlineData("http://localhost:4318/", "http://localhost:4318/v1/traces")]
    [InlineData(" http://alloy:4318/otlp/ ", "http://alloy:4318/otlp/v1/traces")]
    [InlineData("https://alloy.example/otlp", "https://alloy.example/otlp/v1/traces")]
    public void OverHttp_TheSignalPathIsAdded(string endpoint, string expected)
    {
        var settings = Valid();
        settings.Endpoint = endpoint;

        Assert.Equal(expected, TelemetryExportOptions.Create(settings, out _)!.GetSignalEndpoint("traces").AbsoluteUri);
    }

    [Fact]
    public void OverGrpc_TheEndpointIsUsedAsItIs()
    {
        var settings = Valid();
        settings.Endpoint = "http://localhost:4317";
        settings.Protocol = TelemetryProtocol.Grpc;

        Assert.Equal("http://localhost:4317/", TelemetryExportOptions.Create(settings, out _)!.GetSignalEndpoint("traces").AbsoluteUri);
    }

    [Theory]
    [InlineData("localhost:4318")]
    [InlineData("not a url")]
    [InlineData("ftp://localhost:4318")]
    [InlineData("http://user:hunter2@localhost:4318")]
    [InlineData("http://localhost:4318?key=hunter2")]
    [InlineData("http://localhost:4318#hunter2")]
    public void ABadEndpoint_TurnsTheExportOffWithoutRepeatingIt(string endpoint)
    {
        var settings = Valid();
        settings.Endpoint = endpoint;

        Assert.Null(TelemetryExportOptions.Create(settings, out var warnings));
        Assert.NotEmpty(warnings);
        Assert.DoesNotContain(warnings, warning => warning.Contains("hunter2"));
    }

    [Theory]
    [InlineData(0, 60, 1.0, 1.0, "shoko-server")]
    [InlineData(5000, 0, 1.0, 1.0, "shoko-server")]
    [InlineData(5000, 60, 1.5, 1.0, "shoko-server")]
    [InlineData(5000, 60, double.NaN, 1.0, "shoko-server")]
    [InlineData(5000, 60, 1.0, -0.1, "shoko-server")]
    [InlineData(5000, 60, 1.0, 1.0, " ")]
    public void ABadValue_TurnsTheExportOff(int timeout, int interval, double ratio, double jobRatio, string serviceName)
    {
        var settings = Valid();
        settings.ExportTimeoutMs = timeout;
        settings.MetricExportIntervalSeconds = interval;
        settings.TraceSampleRatio = ratio;
        settings.JobTraceSampleRatio = jobRatio;
        settings.ServiceName = serviceName;

        Assert.Null(TelemetryExportOptions.Create(settings, out var warnings));
        Assert.Contains(warnings, warning => warning.Contains("turned off"));
    }

    [Fact]
    public void Headers_AreNormalizedAndKeepEqualsInTheirValues()
    {
        var settings = Valid();
        settings.Headers = " Authorization = Basic abc== , X-Scope-OrgID=me ";

        Assert.Equal("Authorization=Basic abc==,X-Scope-OrgID=me", TelemetryExportOptions.Create(settings, out _)!.Headers);
    }

    [Theory]
    [InlineData("hunter2")]
    [InlineData("=hunter2")]
    [InlineData("key=")]
    [InlineData("a=b,hunter2")]
    [InlineData("key=hun\r\nter2")]
    public void BadHeaders_TurnTheExportOffWithoutRepeatingThem(string headers)
    {
        var settings = Valid();
        settings.Headers = headers;

        Assert.Null(TelemetryExportOptions.Create(settings, out var warnings));
        Assert.DoesNotContain(warnings, warning => warning.Contains("hunter2"));
    }

    [Theory]
    [InlineData("http://alloy:4318", true)]
    [InlineData("http://localhost:4318", false)]
    [InlineData("http://127.0.0.1:4318", false)]
    [InlineData("https://alloy:4318", false)]
    public void HeadersOverPlainHttpToAnotherMachine_AreWarnedAbout(string endpoint, bool warned)
    {
        var settings = Valid();
        settings.Endpoint = endpoint;
        settings.Headers = "Authorization=secret";

        Assert.NotNull(TelemetryExportOptions.Create(settings, out var warnings));
        Assert.Equal(warned, warnings.Count > 0);
    }

    [Fact]
    public void Migration_AddsTheSectionOnlyWhenItIsMissing()
    {
        Assert.NotNull(JObject.Parse(SettingsMigrations.MigrateAddTelemetrySection("{}"))["Telemetry"]);

        var existing = """{"Telemetry":{"Endpoint":"http://localhost:4318"}}""";
        Assert.Equal(existing, SettingsMigrations.MigrateAddTelemetrySection(existing));
    }

    [Fact]
    public void AnUpgradedFile_LoadsWithTheExportOff()
    {
        var settings = LoadUpgradedFile();

        Assert.Empty(settings.Telemetry.Endpoint);
        Assert.Null(TelemetryExportOptions.Create(settings.Telemetry, out _));
    }

    [Fact]
    public void AnUpgradedFile_TakesTheEnvironmentVariablesOfTheNewSection()
    {
        Environment.SetEnvironmentVariable("SHOKO_OTLP_SERVICE_NAME", "from-the-environment");
        try
        {
            Assert.Equal("from-the-environment", LoadUpgradedFile().Telemetry.ServiceName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHOKO_OTLP_SERVICE_NAME", null);
        }
    }

    private ServerSettings LoadUpgradedFile()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
        var path = service.GetConfigurationInfo<ServerSettings>().Path!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, TestData.TestData.ServerSettingsV5.Value);
        return service.Load<ServerSettings>();
    }
}
