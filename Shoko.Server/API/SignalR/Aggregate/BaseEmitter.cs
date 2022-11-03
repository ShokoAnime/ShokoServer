// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace Shoko.Server.API.SignalR.Aggregate;

public abstract class BaseEmitter : IEmitter
{
    public abstract object GetInitialMessage();

    public string GetName(string message)
    {
        var type = GetType().FullName?.Split('.').LastOrDefault()?.Replace("Emitter", "") ?? "Misc";
        return type + ":" + message;
    }
}
