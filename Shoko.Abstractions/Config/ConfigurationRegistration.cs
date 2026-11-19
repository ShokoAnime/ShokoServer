using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Abstractions.Config;

/// <summary>
///   A configuration type a plugin registered with
///   <see cref="ConfigurationRegistrationExtensions.AddConfiguration{TPlugin, TConfig}(IServiceCollection)"/>,
///   left in the service collection for the server to pick up.
/// </summary>
/// <param name="PluginType">The plugin the configuration belongs to.</param>
/// <param name="ConfigurationType">The configuration type.</param>
internal sealed record ConfigurationRegistration(Type PluginType, Type ConfigurationType);
