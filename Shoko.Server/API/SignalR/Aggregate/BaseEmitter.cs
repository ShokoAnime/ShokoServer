// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR.Aggregate;

public abstract class BaseEmitter : IEmitter
{
    protected readonly IHubContext<Hub> Hub;

    protected BaseEmitter(IHubContext<Hub> hub)
    {
        Hub = hub;
    }

    public abstract object GetInitialMessage();

    public async Task SendAsync(string message, params object[] args)
    {
        await Hub.Clients.All.SendCoreAsync(GetName(message), args);
    }

    public string GetName(string message)
    {
        var type = GetType().FullName?.Split('.').LastOrDefault()?.Replace("Emitter", "") ?? "Misc";
        return type + ":" + message;
    }
}
