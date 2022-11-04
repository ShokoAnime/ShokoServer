// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Shoko.Server.API.SignalR.Aggregate;

public interface IEmitter
{
    string Group { get; }
    object GetInitialMessage();
    Task SendAsync(string message, params object[] args);
    string GetName(string message);
}
