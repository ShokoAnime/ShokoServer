// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR.Hubs;

public abstract class BaseEmitter<T> where T : Hub
{
    protected readonly IHubContext<T> Hub;

    public BaseEmitter(IHubContext<T> hub)
    {
        Hub = hub;
    }

    public abstract object GetInitialMessage();

    protected async Task SendAsync(string message, params object[] args)
    {
        await Hub.Clients.All.SendCoreAsync(GetHubName(message), args);
    }

    private string GetHubName(string message)
    {
        var type = GetType().FullName?.Split('.').LastOrDefault()?.Replace("Emitter", "") ?? "Misc";
        return type + ":" + message;
    }
}
