using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Shoko.Tests.Infrastructure;

/// <summary>
///   Registers the SDK's MSBuild once, as the test assembly loads, before any
///   test touches a <c>Microsoft.Build</c> type. Registering from a test class
///   instead races the classes running in parallel: one that loads MSBuild
///   first makes the registration throw.
/// </summary>
internal static class MSBuildRegistration
{
    [ModuleInitializer]
    internal static void Register()
    {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();
    }
}
