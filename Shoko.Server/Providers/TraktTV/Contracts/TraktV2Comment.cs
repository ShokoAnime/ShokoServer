﻿using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktV2Comment
{
    [DataMember(Name = "id")] public int id { get; set; }

    [DataMember(Name = "comment")] public string comment { get; set; }

    [DataMember(Name = "spoiler")] public bool spoiler { get; set; }

    [DataMember(Name = "review")] public bool review { get; set; }

    [DataMember(Name = "parent_id")] public int parent_id { get; set; }

    [DataMember(Name = "created_at")] public string created_at { get; set; }

    public DateTime? CreatedAtDate => TraktTVHelper.GetDateFromUTCString(created_at);

    [DataMember(Name = "replies")] public int replies { get; set; }

    [DataMember(Name = "likes")] public int? likes { get; set; }

    [DataMember(Name = "user_rating")] public int? user_rating { get; set; }

    [DataMember(Name = "user")] public TraktV2User user { get; set; }
}
