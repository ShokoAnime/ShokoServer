using System.Collections.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_HashDigestRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<VideoLocal_HashDigest, int>(databaseFactory)
{
    private PocoIndex<int, VideoLocal_HashDigest, int>? _videoIDs;

    private PocoIndex<int, VideoLocal_HashDigest, (string type, string value)>? _hashTypeAndValues;

    protected override int SelectKey(VideoLocal_HashDigest entity)
        => entity.VideoLocal_HashDigestID;

    public override void PopulateIndexes()
    {
        _videoIDs = Cache.CreateIndex(a => a.VideoLocalID);
        _hashTypeAndValues = Cache.CreateIndex(a => (a.Type, a.Value));
    }

    public IReadOnlyList<VideoLocal_HashDigest> GetByVideoLocalID(int videoLocalID)
        => videoLocalID > 0
            ? _videoIDs!.GetMultiple(videoLocalID)
            : [];

    public IReadOnlyList<VideoLocal_HashDigest> GetByHashTypeAndValue(string hashType, string value)
        => !string.IsNullOrEmpty(hashType) && !string.IsNullOrEmpty(value)
            ? _hashTypeAndValues!.GetMultiple((hashType, value))
            : [];
}
