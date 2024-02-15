using System;

namespace Shoko.Server.Server;

public class ServerAboutToStartEventArgs : EventArgs
{
    public IServiceProvider ServiceProvider { get; init; }
}
