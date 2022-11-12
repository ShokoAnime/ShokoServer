// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Shoko.Server.Services.ConnectivityMon;

public class GeneralConnectivityMonitor : PingConnectivityMonitor
{

    public GeneralConnectivityMonitor() : base("1.1.1.1") // Use Cloudflare DNS server for now. TODO: Make target configurable
    { }
}
