using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Logging.Models;

namespace Shoko.Abstractions.Logging;

/// <summary>
///   A log level rule a plugin registered with
///   <see cref="LogLevelRuleExtensions.AddLogLevelRule{TPlugin}(IServiceCollection, string, Microsoft.Extensions.Logging.LogLevel?, Microsoft.Extensions.Logging.LogLevel?)"/>,
///   left in the service collection for the server to pick up.
/// </summary>
/// <param name="PluginType">The plugin the rule belongs to.</param>
/// <param name="Rule">The rule.</param>
internal sealed record LogLevelRuleRegistration(Type PluginType, LogLevelRule Rule);
