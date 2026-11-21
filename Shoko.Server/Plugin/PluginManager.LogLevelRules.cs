using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Logging;
using Shoko.Abstractions.Logging.Models;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.Plugin;

/// <summary>
///   The default log level rules each plugin brings.
/// </summary>
public partial class PluginManager
{
    #region Log Level Rules

    /// <summary>
    ///   The log level rules plugins registered through
    ///   <see cref="LogLevelRuleExtensions.AddLogLevelRule{TPlugin}(IServiceCollection, string, Microsoft.Extensions.Logging.LogLevel?, Microsoft.Extensions.Logging.LogLevel?, bool)"/>,
    ///   with the plugin whose services registered them.
    /// </summary>
    private readonly List<(LogLevelRule Rule, LocalPluginInfo PluginInfo)> _logLevelRuleRegistrations = [];

    /// <summary>
    ///   Takes the log level rules a plugin's service registration added out
    ///   of the service collection, since only the plugin manager reads them.
    /// </summary>
    /// <param name="pluginInfo">The plugin whose services were registered.</param>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="before">The service descriptors before the plugin registered its services.</param>
    /// <exception cref="InvalidOperationException">A rule names another plugin.</exception>
    private void TakeLogLevelRuleRegistrations(LocalPluginInfo pluginInfo, IServiceCollection serviceCollection, IReadOnlyCollection<ServiceDescriptor> before)
    {
        var descriptors = serviceCollection
            .Except(before)
            .Where(descriptor => descriptor.ImplementationInstance is LogLevelRuleRegistration)
            .ToList();
        var registrations = descriptors.ConvertAll(descriptor => (LogLevelRuleRegistration)descriptor.ImplementationInstance!);
        if (registrations.FirstOrDefault(registration => registration.PluginType != pluginInfo.PluginType) is { } foreign)
            throw new InvalidOperationException($"It registered the log level rule \"{foreign.Rule.LoggerNamePattern}\" for the plugin type \"{foreign.PluginType.FullName}\", which is not its own.");

        foreach (var descriptor in descriptors)
            serviceCollection.Remove(descriptor);
        _logLevelRuleRegistrations.AddRange(registrations.Select(registration => (registration.Rule, pluginInfo)));
    }

    /// <summary>
    ///   The log level rules of the active plugins that have not failed, in
    ///   load order.
    /// </summary>
    /// <remarks>
    ///   A registration was taken before the plugin was initialized, which put
    ///   a new info in its place, so it is read through the current one.
    /// </remarks>
    private List<LogLevelRuleInfo> GetPluginLogLevelRules()
        => _logLevelRuleRegistrations
            .Select(registration => (registration.Rule, PluginInfo: _pluginTypes[registration.PluginInfo.LoadOrder]))
            .Where(registration => registration.PluginInfo is { IsActive: true, FailureReason: null })
            .OrderBy(registration => registration.PluginInfo.LoadOrder)
            .Select(registration => new LogLevelRuleInfo
            {
                Rule = registration.Rule,
                Source = LogLevelRuleSource.Plugin,
                PluginID = registration.PluginInfo.ID,
                PluginName = registration.PluginInfo.Name,
            })
            .ToList();

    #endregion
}
