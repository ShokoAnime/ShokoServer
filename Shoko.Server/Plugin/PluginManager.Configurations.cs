using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.Plugin;

/// <summary>
///   The configuration types each plugin brings: those in its main assembly,
///   its declared main configuration, and those it registered from the other
///   assemblies it loaded itself.
/// </summary>
public partial class PluginManager
{
    #region Configurations

    /// <summary>
    ///   The configuration types plugins registered through
    ///   <see cref="ConfigurationRegistrationExtensions.AddConfiguration{TPlugin, TConfig}(IServiceCollection)"/>,
    ///   with the plugin whose services registered them.
    /// </summary>
    private readonly List<(Type ConfigurationType, LocalPluginInfo PluginInfo)> _configurationRegistrations = [];

    /// <summary>
    ///   Takes the configuration registrations a plugin's service
    ///   registration added out of the service collection, since only the
    ///   plugin manager reads them, and the configuration types its user
    ///   configurations name, which count as registered by it too.
    /// </summary>
    /// <param name="pluginInfo">The plugin whose services were registered.</param>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="before">The service descriptors before the plugin registered its services.</param>
    /// <exception cref="InvalidOperationException">A registration, or a user configuration's, names another plugin.</exception>
    private void TakeConfigurationRegistrations(LocalPluginInfo pluginInfo, IServiceCollection serviceCollection, IReadOnlyCollection<ServiceDescriptor> before)
    {
        var descriptors = serviceCollection
            .Except(before)
            .Where(descriptor => descriptor.ImplementationInstance is ConfigurationRegistration)
            .ToList();
        var registrations = descriptors.ConvertAll(descriptor => (ConfigurationRegistration)descriptor.ImplementationInstance!);
        if (registrations.FirstOrDefault(registration => registration.PluginType != pluginInfo.PluginType) is { } foreign)
            throw new InvalidOperationException($"It registered the configuration \"{foreign.ConfigurationType.FullName}\" for the plugin type \"{foreign.PluginType.FullName}\", which is not its own.");

        var userRegistrations = serviceCollection
            .Except(before)
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<UserConfigurationRegistration>()
            .ToList();
        if (userRegistrations.FirstOrDefault(registration => registration.PluginType != pluginInfo.PluginType) is { } foreignUser)
            throw new InvalidOperationException($"It registered the user configuration \"{foreignUser.ID}\" for the plugin type \"{foreignUser.PluginType.FullName}\", which is not its own.");

        foreach (var descriptor in descriptors)
            serviceCollection.Remove(descriptor);
        _configurationRegistrations.AddRange(registrations.Select(registration => (registration.ConfigurationType, pluginInfo)));
        _configurationRegistrations.AddRange(userRegistrations
            .Where(registration => registration.ConfigurationType is not null)
            .Select(registration => (registration.ConfigurationType!, pluginInfo)));
    }

    /// <summary>
    ///   Gathers the configuration types of every active plugin, each with
    ///   the plugin it belongs to.
    /// </summary>
    /// <remarks>
    ///   A plugin's main assembly is scanned. Its declared main configuration
    ///   (<see cref="IPlugin{TConfiguration}"/>) and its registered types are
    ///   added from any other assembly it loaded itself. A declared or
    ///   registered type from an assembly the plugin does not own, or that
    ///   cannot be a configuration, fails the plugin.
    /// </remarks>
    /// <returns>The configuration types, each once.</returns>
    private List<(Type ConfigurationType, LocalPluginInfo PluginInfo)> GetConfigurationTypes()
    {
        var types = GetTypes<IConfiguration>()
            .Select(type => (ConfigurationType: type, PluginInfo: GetPluginInfo(type.Assembly)!))
            .ToList();
        var seen = types.Select(entry => entry.ConfigurationType).ToHashSet();

        var declared = _pluginTypes
            .Where(pluginInfo => pluginInfo is { IsActive: true, PluginType: not null })
            .SelectMany(pluginInfo => pluginInfo.PluginType!.GetInterfaces()
                .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IPlugin<>))
                .Select(type => (ConfigurationType: type.GenericTypeArguments[0], PluginInfo: pluginInfo)));
        // A registration was taken before the plugin was initialized, which
        // put a new info in its place, so it is read through the current one.
        var registered = _configurationRegistrations
            .Select(registration => (registration.ConfigurationType, PluginInfo: _pluginTypes[registration.PluginInfo.LoadOrder]));
        foreach (var (configurationType, pluginInfo) in declared.Concat(registered).ToList())
        {
            if (!pluginInfo.IsActive || pluginInfo.FailureReason is not null || seen.Contains(configurationType))
                continue;

            if (!IsConcreteTypeOf<IConfiguration>(configurationType))
            {
                MarkFailed(pluginInfo, "registering its configurations", new InvalidOperationException($"The configuration \"{configurationType.FullName}\" is not a concrete configuration type."));
                continue;
            }

            if (GetOwningPluginInfo(configurationType.Assembly) is not { } owner || owner.ID != pluginInfo.ID)
            {
                var whose = GetOwningPluginInfo(configurationType.Assembly) is { } other ? $"the plugin \"{other.Name}\"" : "no plugin";
                MarkFailed(pluginInfo, "registering its configurations", new InvalidOperationException($"The configuration \"{configurationType.FullName}\" is in an assembly of {whose}, not one it loaded itself."));
                continue;
            }

            seen.Add(configurationType);
            types.Add((configurationType, pluginInfo));
        }

        return types;
    }

    #endregion
}
