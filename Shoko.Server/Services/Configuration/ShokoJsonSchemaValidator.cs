using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Services.Configuration;

public partial class ShokoJsonSchemaValidator<TConfig>(ILogger logger, ConfigurationService configurationService, ConfigurationInfo info, TConfig? config, bool saveValidation, bool loadValidation, JsonSchemaValidatorSettings? settings = null) : JsonSchemaValidatorBase(settings) where TConfig : class, IConfiguration, new()
{
    private readonly ILogger _logger = logger;

    private readonly ConfigurationService _configurationService = configurationService;

    private readonly ConfigurationInfo _info = info;

    private readonly TConfig? _config = config;

    private readonly bool _saveValidation = saveValidation;

    private readonly bool _loadValidation = loadValidation;

    private readonly Dictionary<string, (string Override, string? Original)> _loadedEnvironmentVariables = !loadValidation && configurationService.InternalLoadedEnvironmentVariables.TryGetValue(info.ID, out var lev0) ? lev0.ToDictionary() : [];

    private readonly Dictionary<string, string?> _restartPending = saveValidation && configurationService.InternalRestartPendingFor.TryGetValue(info.ID, out var rr0) ? rr0.ToDictionary() : [];

    private JToken? _existingData;

    public (JToken Token, ICollection<ValidationError> Errors) Validate(string jsonData)
    {
        var (token, errors) = Validate(jsonData, _info.Schema);
        if (errors.Count == 0)
        {
            if (_loadValidation)
            {
                if (_loadedEnvironmentVariables.Count > 0)
                    _configurationService.InternalLoadedEnvironmentVariables[_info.ID] = _loadedEnvironmentVariables;
                else
                    _configurationService.InternalLoadedEnvironmentVariables.Remove(_info.ID);
            }
            if (_saveValidation)
            {
                if (_restartPending.Count > 0)
                    _configurationService.InternalRestartPendingFor[_info.ID] = _restartPending;
                else
                    _configurationService.InternalRestartPendingFor.Remove(_info.ID);
            }
        }

        return (token, errors);
    }

    [GeneratedRegex(@"(?<!\\)""")]
    private static partial Regex InvalidQuoteRegex();

    protected override ICollection<ValidationError> Validate(JToken? parentToken, JToken? token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath)
    {
        var errors = new List<ValidationError>();
        if ((schema.ExtensionData?.TryGetValue(ConfigurationService.UiDefinition, out var prop0) ?? false) && prop0 is Dictionary<string, object?> uiDefinition)
        {
            if (_loadValidation)
            {
                if (uiDefinition.TryGetValue(ConfigurationService.ElementEnvironmentVariable, out var prop1) && prop1 is string envVarName && Environment.GetEnvironmentVariable(envVarName) is string envVar)
                {
                    if (_loadedEnvironmentVariables.ContainsKey(envVarName))
                    {
                        errors.Add(new ValidationError((ValidationErrorKind)1_003, propertyName, propertyPath, token, schema));
                    }
                    else if (base.Validate(parentToken, token, schema, schemaType, propertyName, propertyPath).Count == 0)
                    {
                        if (schema.Type.HasFlag(JsonObjectType.String) && (envVar.Length < 2 || envVar[0] != '"' || envVar[^1] != '"') && (!schema.Type.HasFlag(JsonObjectType.Null) || envVar != "null"))
                            envVar = '"' + envVar.Replace(InvalidQuoteRegex(), "\\\"") + '"';

                        try
                        {
                            var parsedToken = JToken.Parse(envVar);
                            _loadedEnvironmentVariables.Add(envVarName, (parsedToken.ToJson(), token?.ToJson()));
                            if (token is not null)
                                token.Replace(parsedToken);
                            else
                                ((JObject)parentToken!).Add(propertyName!, parsedToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to parse environment variable '{EnvVar}'.", envVar);
                            errors.Add(new ValidationError((ValidationErrorKind)1_002, propertyName, propertyPath, token, schema));
                        }
                    }
                }
            }
            else
            {
                if (
                    uiDefinition.TryGetValue(ConfigurationService.ElementEnvironmentVariable, out var prop1) &&
                    uiDefinition[ConfigurationService.ElementEnvironmentVariableOverridable] is false &&
                    prop1 is string envVarName &&
                    _loadedEnvironmentVariables.TryGetValue(envVarName, out var tuple)
                )
                {
                    // If the value is different from what we expect, then add an error, otherwise
                    if (!string.Equals(tuple.Override, token?.ToJson(), StringComparison.Ordinal))
                    {
                        errors.Add(new ValidationError((ValidationErrorKind)1_001, propertyName, propertyPath, token, schema));
                    }
                    // remove or revert the token if we're saving and the override is different from the original value.
                    else if (_saveValidation && !string.Equals(tuple.Original, tuple.Override, StringComparison.Ordinal))
                    {
                        if (tuple.Original is null)
                            ((JObject)parentToken!).Remove(propertyName!);
                        else
                            token!.Replace(JToken.Parse(tuple.Original));
                    }
                }

                if (_saveValidation && uiDefinition.TryGetValue(ConfigurationService.ElementRequiresRestart, out var prop2) && prop2 is true)
                {
                    if (_restartPending.TryGetValue(propertyPath, out var existingValue))
                    {
                        if (string.Equals(existingValue, token?.ToJson(), StringComparison.Ordinal))
                            _restartPending.Remove(propertyPath);
                    }
                    else
                    {
                        existingValue = GetPropertyPath(propertyPath)?.ToJson();
                        if (!string.Equals(existingValue, token?.ToJson(), StringComparison.Ordinal))
                            _restartPending.Add(propertyPath, existingValue);
                    }
                }
            }
        }
        errors.AddRange(base.Validate(parentToken, token, schema, schemaType, propertyName, propertyPath));
        return errors;
    }

    private JToken? GetPropertyPath(string propertyPath)
    {
        _existingData ??= JToken.Parse(_configurationService.Serialize(_config ?? _configurationService.Load<TConfig>(false)));
        return _existingData.SelectToken(propertyPath);
    }
}
