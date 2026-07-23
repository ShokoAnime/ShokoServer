using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Shoko.BuildTools;

/// <summary>
///   Ensures MSBuildLocator is registered before any MSBuild types are
///   loaded by the runtime. This runs at module initialization time, before
///   Main or any static constructors in the assembly.
/// </summary>
internal static class MSBuildRegistration
{
    [ModuleInitializer]
    public static void RegisterMSBuild()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            try
            {
                MSBuildLocator.RegisterDefaults();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to register MSBuildLocator: {ex.Message}");
            }
        }
    }
}
