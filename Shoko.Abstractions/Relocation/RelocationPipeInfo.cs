using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Services;

namespace Shoko.Abstractions.Relocation;

/// <summary>
///   An info object representing a relocation pipe with extra helpers utilizing
///   the services.
/// </summary>
/// <param name="relocationService">The relocation service.</param>
/// <param name="configurationService">The configuration service.</param>
/// <param name="pipe">The stored relocation pipe to get the initial details from.</param>
public class RelocationPipeInfo(IRelocationService relocationService, IConfigurationService configurationService, IRelocationPipe pipe) : IStoredRelocationPipe
{
    private readonly IRelocationService _relocationService = relocationService;

    private readonly IConfigurationService _configurationService = configurationService;

    /// <inheritdoc/>
    public Guid ID { get; init; } = pipe is IStoredRelocationPipe stored ? stored.ID : Guid.Empty;

    /// <inheritdoc/>
    public Guid ProviderID { get; init; } = pipe.ProviderID;

    /// <inheritdoc/>
    public string Name { get; set; } = pipe is IStoredRelocationPipe stored ? stored.Name : string.Empty;

    /// <inheritdoc/>
    public byte[]? Configuration { get; private set; } = pipe.Configuration;

    /// <inheritdoc/>
    public bool IsDefault { get; set; } = pipe is IStoredRelocationPipe stored && stored.IsDefault;

    /// <summary>
    ///   Gets the relocation pipe.
    /// </summary>
    public IRelocationPipe Pipe { get; set; } = pipe;

    private RelocationProviderInfo? _providerInfo;

    /// <summary>
    ///   Gets the provider info for the given <see cref="ProviderID"/>, if
    ///   available in the current runtime environment.
    /// </summary>
    public RelocationProviderInfo? ProviderInfo
        => _providerInfo ??= _relocationService.GetProviderInfo(ProviderID);

    /// <summary>
    ///   Attempts to load the configuration for the stored relocation pipe.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the provider is currently unavailable or does not support
    ///   a configuration.
    /// </exception>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when the configuration fails validation.
    /// </exception>
    /// <returns></returns>
    public IConfiguration LoadConfiguration()
    {
        if (ProviderInfo is not { } providerInfo)
            throw new InvalidOperationException("Cannot load the configuration for a pipe with an unregistered provider.");

        if (providerInfo.ConfigurationInfo is null)
            throw new InvalidOperationException("Cannot load the configuration for a provider that does not support one.");

        if (Configuration is null)
        {
            Configuration = Encoding.UTF8.GetBytes(_configurationService.Serialize(_configurationService.New(providerInfo.ConfigurationInfo)));
            _relocationService.UpdatePipe(this);
        }

        var configuration = _configurationService.Deserialize(providerInfo.ConfigurationInfo, Encoding.UTF8.GetString(Configuration));
        var validationErrors = _configurationService.Validate(providerInfo.ConfigurationInfo, configuration);
        if (validationErrors.Count > 0)
            throw new ConfigurationValidationException("load", providerInfo.ConfigurationInfo, validationErrors);

        return configuration;
    }

    /// <summary>
    ///   Save the configuration for the stored relocation pipe.
    /// </summary>
    /// <param name="json">
    ///   The stringified JSON of the configuration to save. Can be null if the
    ///   provider does not currently support a configuration.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when trying to set a configuration for a provider that does not
    ///   support one, or unset a configuration for a provider that needs one.
    /// </exception>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when the configuration fails validation.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the configuration was saved, <c>false</c> otherwise.
    /// </returns>
    public bool SaveConfiguration(string? json)
    {
        if (ProviderInfo is not { } providerInfo)
            return false;
        if (providerInfo.ConfigurationInfo is not { } configurationInfo)
        {
            if (json is not null)
                throw new InvalidCastException("Cannot set a configuration for a provider that does not support one.");

            if (Configuration is null)
                return false;

            Configuration = null;
            _relocationService.UpdatePipe(this);
            return true;
        }

        if (json is null)
            throw new InvalidOperationException("Cannot unset a configuration for a provider that does needs one.");

        var validationErrors = _configurationService.Validate(providerInfo.ConfigurationInfo, json);
        if (validationErrors.Count > 0)
            throw new ConfigurationValidationException("save", providerInfo.ConfigurationInfo, validationErrors);

        // We deserialize it and serialize it again so the json always follows the configuration service's serialization rules.
        var configuration = _configurationService.Deserialize(providerInfo.ConfigurationInfo, json);
        var configurationBytes = Encoding.UTF8.GetBytes(_configurationService.Serialize(configuration));
        if (Configuration is null || !configurationBytes.SequenceEqual(Configuration))
        {
            Configuration = configurationBytes;
            _relocationService.UpdatePipe(this);
            return true;
        }

        return false;
    }

    /// <summary>
    ///   Save the configuration for the stored relocation pipe.
    /// </summary>
    /// <param name="configuration">
    ///   The configuration to save.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when trying to set a configuration for a provider that does not
    ///   support one, or unset a configuration for a provider that needs one.
    /// </exception>
    /// <exception cref="InvalidCastException">
    ///   Thrown when the provided configuration is not of the same type as the
    ///   provider expects.
    /// </exception>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when the configuration fails validation.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the configuration was saved, <c>false</c> otherwise.
    /// </returns>
    public bool SaveConfiguration(IConfiguration? configuration)
    {
        if (ProviderInfo is not { } providerInfo)
            return false;

        if (providerInfo.ConfigurationInfo is not { } configurationInfo)
        {
            if (configuration is not null)
                throw new InvalidCastException("Cannot set a configuration for a provider that does not support one.");

            if (Configuration is null)
                return false;

            Configuration = null;
            _relocationService.UpdatePipe(this);
            return true;
        }

        if (configuration is null)
            throw new InvalidOperationException("Cannot unset a configuration for a provider that does needs one.");

        if (configuration.GetType() != configurationInfo.Type)
            throw new InvalidCastException("The provided configuration is not of the same type as the provider expects.");

        var validationErrors = _configurationService.Validate(providerInfo.ConfigurationInfo, configuration);
        if (validationErrors.Count > 0)
            throw new ConfigurationValidationException("save", providerInfo.ConfigurationInfo, validationErrors);

        var configurationBytes = Encoding.UTF8.GetBytes(_configurationService.Serialize(configuration));
        if (Configuration is null || !((IEnumerable<byte>)configurationBytes).SequenceEqual(Configuration))
        {
            Configuration = configurationBytes;
            _relocationService.UpdatePipe(this);
            return true;
        }

        return false;
    }
}
