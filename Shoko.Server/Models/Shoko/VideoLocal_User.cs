using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Shoko;

public class VideoLocal_User : IVideoUserData
{
    public int VideoLocal_UserID { get; set; }

    public int JMMUserID { get; set; }

    public int VideoLocalID { get; set; }

    public DateTime? WatchedDate { get; set; }

    public long ResumePosition { get; set; }

    public DateTime LastUpdated { get; set; }

    public int WatchedCount { get; set; }

    public int? LastVideoStreamIndex { get; set; }

    public int? LastAudioStreamIndex { get; set; }

    public int? LastSubtitleStreamIndex { get; set; }

#pragma warning disable IDE0044 // Add readonly modifier - it's set by NHibernate
    private Dictionary<string, JToken> _clientData = [];
#pragma warning restore IDE0044

    /// <summary>
    /// Where to resume the playback of the <see cref="Shoko.VideoLocal"/>
    ///  as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan? ProgressPosition
    {
        get => ResumePosition > 0 ? TimeSpan.FromMilliseconds(ResumePosition) : null;
        set => ResumePosition = value.HasValue ? (long)Math.Round(value.Value.TotalMilliseconds) : 0;
    }

    /// <summary>
    /// Gets a read-only, deep-cloned view of the client-specific data bag.
    /// Values are cloned so callers cannot mutate the cached entity in place.
    /// </summary>
    public IReadOnlyDictionary<string, JToken> ClientData
        => new ReadOnlyDictionary<string, JToken>(_clientData.ToDictionary(kv => kv.Key, kv => kv.Value.DeepClone()));

    /// <summary>
    /// Gets a deep-cloned client data value by key, or <c>null</c> if the key
    /// is not present.
    /// </summary>
    public JToken? GetClientData(string clientKey)
        => _clientData.TryGetValue(clientKey, out var v) ? v.DeepClone() : null;

    /// <summary>
    /// Gets a deserialized client data value by key.
    /// </summary>
    public T? GetClientData<T>(string clientKey)
    {
        if (!_clientData.TryGetValue(clientKey, out var v)) return default;
        try { return v.ToObject<T>(); }
        catch { return default; }
    }

    /// <summary>
    /// Sets or removes a client data entry. Pass a <c>null</c> reference to
    /// remove the key; a non-null <see cref="JToken"/> (including JSON null) is
    /// stored as a deep clone.
    /// </summary>
    internal void SetClientDataInternal(string key, JToken? value)
    {
        if (value is null)
            _clientData.Remove(key);
        else
            _clientData[key] = value.DeepClone();
    }

    /// <summary>
    /// Clears all client data entries.
    /// </summary>
    internal void ClearClientDataInternal() => _clientData.Clear();

    public JMMUser User
        => RepoFactory.JMMUser.GetByID(JMMUserID);

    /// <summary>
    /// Get the related <see cref="Shoko.VideoLocal"/>.
    /// </summary>
    public VideoLocal? VideoLocal
        => RepoFactory.VideoLocal.GetByID(VideoLocalID);

    public override string ToString()
    {
        var video = VideoLocal;
        if (video == null)
            return $"{VideoLocalID} -- User {JMMUserID}";

#pragma warning disable CS0618
        return $"{video.FileName} --- {video.Hash} --- User {JMMUserID}";
#pragma warning restore CS0618
    }

    #region IUserData Implementation

    int IUserData.UserID => JMMUserID;

    DateTime IUserData.LastUpdatedAt => LastUpdated;

    IUser IUserData.User => User ??
        throw new NullReferenceException($"Unable to find IUser with the given id. (User={JMMUserID})");

    #endregion

    #region IVideoUserData Implementation

    int IVideoUserData.VideoID => VideoLocalID;

    int IVideoUserData.PlaybackCount => WatchedCount;

    TimeSpan IVideoUserData.ProgressPosition => ProgressPosition ?? TimeSpan.Zero;

    DateTime? IVideoUserData.LastPlayedAt => WatchedDate;

    IVideo? IVideoUserData.Video => VideoLocal;

    int? IVideoUserData.LastVideoStreamIndex => LastVideoStreamIndex;

    int? IVideoUserData.LastAudioStreamIndex => LastAudioStreamIndex;

    int? IVideoUserData.LastSubtitleStreamIndex => LastSubtitleStreamIndex;

    IReadOnlyDictionary<string, JToken> IVideoUserData.ClientData => ClientData;

    #endregion
}
