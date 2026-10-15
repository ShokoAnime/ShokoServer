using System;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Relocation;

namespace Shoko.Tests.Plugin.WebAOM;

/// <summary>
///   Stands in for the WebAOM plugin, which lives in its own repository, so
///   the core's carry-over of the renamer's settings can be checked without it.
/// </summary>
public class WebAOMPlugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID => new("8c8d75f2-cd0c-5c0b-9a9e-8c2c6d3e5f1a");

    /// <inheritdoc/>
    public string Name => "WebAOM";

    /// <inheritdoc/>
    public string Description => string.Empty;
}

/// <summary>
///   Stands in for the WebAOM renamer: only its settings type is read on the
///   paths under test.
/// </summary>
public class WebAOMRenamer : IRelocationProvider<WebAOMSettings>
{
    /// <inheritdoc/>
    public string Name => "WebAOM Renamer";

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always; the tests never rename.</exception>
    public RelocationResult GetPath(RelocationContext<WebAOMSettings> context)
        => throw new NotSupportedException();
}
