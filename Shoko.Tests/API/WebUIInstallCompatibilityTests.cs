using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Web;
using Shoko.Abstractions.Web.Enums;
using Shoko.Abstractions.Web.Services;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Installing or updating the web UI refuses a release that cannot be used,
/// with a <c>409</c> saying why, unless incompatible releases are allowed.
/// </summary>
public class WebUIInstallCompatibilityTests
{
    private static readonly WebUIReleaseInformation _incompatible = new()
    {
        Version = new(0, 1, 0),
        Description = null,
        SourceRevision = "abc",
        ReleaseTag = "web-v0.1.0",
        Channel = ReleaseChannel.Stable,
        ReleasedAt = DateTime.UnixEpoch,
        IncompatibleReason = "It needs server version 6.0.0-dev.91 or newer.",
    };

    private readonly Mock<IWebUIService> _service = new();

    public WebUIInstallCompatibilityTests()
    {
        _service.Setup(service => service.GetReleases(It.IsAny<ReleaseChannel?>(), It.IsAny<bool>(), It.IsAny<WebUIProviderInfo?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebUIReleaseInformation> { _incompatible });
        _service.Setup(service => service.GetLatestRelease(It.IsAny<ReleaseChannel>(), It.IsAny<bool>(), false, It.IsAny<WebUIProviderInfo?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebUIReleaseInformation?)null);
        _service.Setup(service => service.GetLatestRelease(It.IsAny<ReleaseChannel>(), It.IsAny<bool>(), true, It.IsAny<WebUIProviderInfo?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_incompatible);
        _service.Setup(service => service.InstallRelease(It.IsAny<WebUIReleaseInformation>(), It.IsAny<WebUIProviderInfo?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebUIInstallResult.Installed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0.1.0")]
    public async Task AnIncompatibleReleaseIsRefusedWithAConflict(string? version)
    {
        var result = Assert.IsType<ObjectResult>(await Controller().UpdateWebUI(ReleaseChannel.Stable, version));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("no-compatible-release", problem.Type);
        Assert.Contains(_incompatible.IncompatibleReason!, problem.Detail);
        _service.Verify(service => service.InstallRelease(It.IsAny<WebUIReleaseInformation>(), It.IsAny<WebUIProviderInfo?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0.1.0")]
    public async Task AnIncompatibleReleaseIsInstalledWhenAllowed(string? version)
    {
        Assert.IsType<NoContentResult>(await Controller().UpdateWebUI(ReleaseChannel.Stable, version, allowIncompatible: true));

        _service.Verify(service => service.InstallRelease(It.IsAny<WebUIReleaseInformation>(), It.IsAny<WebUIProviderInfo?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private WebUIController Controller()
        => new(Mock.Of<ISettingsProvider>(), null!, _service.Object, null!, null!, NullLogger<WebUIController>.Instance, null!, null!)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() },
        };
}
