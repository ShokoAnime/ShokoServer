using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;

#nullable enable
namespace Shoko.Server.Extensions;

public static class DependencyInjectionExtensions
{
    public static TReturn? Invoke<TReturn>(
        this MethodInfo method,
        IPluginManager pluginManager,
        object target,
        IEnumerable<object?> manualArgs)
    {
        var parameters = method.GetParameters();
        var manualList = manualArgs.WhereNotNull().ToList();
        var arguments = new object?[parameters.Length];
        var resolutionErrors = new List<Exception>();
        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            switch (param)
            {
                case { ParameterType: var t } when manualList.FirstOrDefault(t.IsInstanceOfType) is { } match:
                    arguments[i] = match;
                    manualList.Remove(match);
                    break;

                case { ParameterType: var t } when pluginManager.GetService(t) is { } service:
                    arguments[i] = service;
                    break;

                case { HasDefaultValue: true, DefaultValue: var def }:
                    arguments[i] = def;
                    break;

                case { IsOptional: true }:
                    arguments[i] = null;
                    break;

                default:
                    resolutionErrors.Add(
                        new InvalidOperationException(
                            $"Could not resolve parameter '{param.Name}' of type '{param.ParameterType.FullName}' for method '{method.DeclaringType?.FullName}.{method.Name}'."
                        )
                    );
                    break;
            }
        }
        if (resolutionErrors.Count > 0)
            throw new AggregateException("One or more parameters could not be resolved.", resolutionErrors);
        var result = method.Invoke(target, arguments);
        if (result is Task<TReturn> task)
            return task.GetAwaiter().GetResult();
        if (result is TReturn typedResult)
            return typedResult;
        return default;
    }
}
