using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_HashDigestRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<VideoLocal_HashDigest, int>(databaseFactory)
{
    private PocoIndex<int, VideoLocal_HashDigest, int>? _videoIDs;

    private PocoIndex<int, VideoLocal_HashDigest, (int videoID, string hashType)>? _videoIDAndHashTypes;

    private PocoIndex<int, VideoLocal_HashDigest, string>? _hashTypes;

    private PocoIndex<int, VideoLocal_HashDigest, (string type, string value)>? _hashTypeAndValues;

    protected override int SelectKey(VideoLocal_HashDigest entity)
        => entity.VideoLocal_HashDigestID;

    public override void PopulateIndexes()
    {
        _videoIDs = Cache.CreateIndex(a => a.VideoLocalID);
        _videoIDAndHashTypes = Cache.CreateIndex(a => (a.VideoLocalID, a.Type));
        _hashTypes = Cache.CreateIndex(a => a.Type);
        _hashTypeAndValues = Cache.CreateIndex(a => (a.Type, a.Value));
    }

    public IReadOnlyList<VideoLocal_HashDigest> GetByVideoLocalID(int videoLocalID)
        => videoLocalID > 0
            ? ReadLock(() => _videoIDs!.GetMultiple(videoLocalID))
            : [];

    public IReadOnlyList<VideoLocal_HashDigest> GetByHashType(string hashType)
        => !string.IsNullOrEmpty(hashType)
            ? ReadLock(() => _hashTypes!.GetMultiple(hashType))
            : [];

    public IReadOnlyList<VideoLocal_HashDigest> GetByVideoIDAndHashType(int videoLocalID, string hashType)
        => videoLocalID > 0 && !string.IsNullOrEmpty(hashType)
            ? ReadLock(() => _videoIDAndHashTypes!.GetMultiple((videoLocalID, hashType)))
            : [];

    public IReadOnlyList<VideoLocal_HashDigest> GetByHashTypeAndValue(string hashType, string value)
        => !string.IsNullOrEmpty(hashType) && !string.IsNullOrEmpty(value)
            ? ReadLock(() => _hashTypeAndValues!.GetMultiple((hashType, value)))
            : [];
}
