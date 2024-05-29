namespace Shoko.Plugin.Abstractions.Services;

public interface IAniDBService
{
    /// <summary>
    /// Is the AniDB UDP API currently reachable?
    /// </summary>
    public bool IsAniDBUdpReachable { get; }
    /// <summary>
    /// Are we currently banned from using the AniDB HTTP API?
    /// </summary>
    public bool IsAniDBHttpBanned { get; }
    /// <summary>
    /// Are we currently banned from using the AniDB UDP API?
    /// </summary>
    public bool IsAniDBUdpBanned { get; }
}
