﻿using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class VideoLocalMap : ClassMap<SVR_VideoLocal>
{
    public VideoLocalMap()
    {
        Table("VideoLocal");
        Not.LazyLoad();
        Id(x => x.VideoLocalID);
        Map(x => x.DateTimeUpdated).Not.Nullable();
        Map(x => x.DateTimeCreated).Not.Nullable();
        Map(x => x.FileName).Not.Nullable();
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.Hash).Not.Nullable();
        Map(x => x.CRC32);
        Map(x => x.MD5);
        Map(x => x.SHA1);
        Map(x => x.HashSource).Not.Nullable();
        Map(x => x.IsIgnored).Not.Nullable();
        Map(x => x.IsVariation).Not.Nullable();
        Map(x => x.MediaVersion).Not.Nullable();
        Map(x => x.MediaBlob).Nullable().CustomType("BinaryBlob");
        Map(x => x.MediaSize).Not.Nullable();
        Map(x => x.MyListID).Not.Nullable();
    }
}
