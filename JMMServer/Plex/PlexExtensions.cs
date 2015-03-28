﻿using System.Collections.Generic;
using JMMContracts;
using JMMContracts.PlexContracts;
using JMMServer.ImageDownload;
// ReSharper disable FunctionComplexityOverflow
namespace JMMServer.Plex
{
    public static class PlexExtensions
    {


        public static string GenPoster(this ImageDetails im)
        {
            if (im == null)
                return null;
            return PlexHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetThumb/" + (int)im.ImageType + "/" + im.ImageID + "/0.6667");
        }

        public static string GenArt(this ImageDetails im)
        {
            if (im == null)
                return null;
            return PlexHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetImage/" + (int)im.ImageType + "/" + im.ImageID);
        }

        public static string GenPoster(this Contract_AniDB_Anime_DefaultImage im)
        {
            if (im == null)
                return null;

            return PlexHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetThumb/" + im.ImageType + "/" + im.AnimeID + "/0.6667");
        }

        public static string GenArt(this Contract_AniDB_Anime_DefaultImage im)
        {
            if (im == null)
                return null;

            return PlexHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetImage/" + im.ImageType + "/" + im.AnimeID);
        }


        public static Hub Clone(this Hub o)
        {
            Hub h = new Hub();
            h.HubIdentifier = o.HubIdentifier;
            h.Key = PlexHelper.ReplaceSchemeHost(o.Key);
            h.More = o.More;
            h.Size = o.Size;
            h.Title = o.Title;
            h.Type = o.Type;
            return h;
        }

        public static Video Clone(this Video o)
        {
            Video v;
            if (o is Directory)
                v = new Directory();
            else
                v = new Video();
            v.AddedAt = o.AddedAt;
            v.AirDate = o.AirDate;
            v.Art = PlexHelper.ReplaceSchemeHost(o.Art);
            v.ChapterSource = o.ChapterSource;
            v.ContentRating = o.ContentRating;
            v.Duration = o.Duration;
            v.EpNumber = o.EpNumber;
            v.EpisodeCount = o.EpisodeCount;
            v.ExtraType = o.ExtraType;
            if (o.Extras != null)
            {
                v.Extras = new Extras();
                v.Extras.Size = o.Extras.Size;
                if (o.Extras.Videos != null)
                {
                    v.Extras.Videos = new List<Video>();
                    o.Extras.Videos.ForEach(a => v.Extras.Videos.Add(a.Clone()));
                }
            }
            v.Genres = o.Genres;
            v.GrandparentKey = o.GrandparentKey;
            v.GrandparentRatingKey = o.GrandparentRatingKey;
            v.GrandparentThumb = o.GrandparentThumb;
            v.GrandparentTitle = o.GrandparentTitle;
            v.Group = o.Group;
            v.Guid = o.Guid;
            v.Index = o.Index;
            v.Key = PlexHelper.ReplaceSchemeHost(o.Key);
            v.LeafCount = o.LeafCount;
            v.Medias = o.Medias;
            v.OriginalTitle = o.OriginalTitle;
            v.OriginallyAvailableAt = o.OriginallyAvailableAt;
            v.ParentIndex = o.ParentIndex;
            v.ParentKey = o.ParentKey;
            v.ParentRatingKey = o.ParentRatingKey;
            v.ParentThumb = o.ParentThumb;
            v.ParentTitle = o.ParentTitle;
            v.PrimaryExtraKey = o.PrimaryExtraKey;
            v.Rating = o.Rating;
            v.RatingKey = o.RatingKey;
            if (o.Related != null)
            {
                v.Related = new List<Hub>();
                o.Related.ForEach(a => v.Related.Add(a.Clone()));
            }
            v.Roles = o.Roles;
            v.Season = o.Season;
            v.SourceTitle = o.SourceTitle;
            v.Summary = o.Summary;
            v.Tagline = o.Tagline;
            v.Tags = o.Tags;
            v.Thumb = PlexHelper.ReplaceSchemeHost(o.Thumb);
            v.Title = o.Title;
            v.Type = o.Type;
            v.UpdatedAt = o.UpdatedAt;
            v.Url = PlexHelper.ReplaceSchemeHost(o.Url);
            v.ViewCount = o.ViewCount;
            v.ViewOffset = o.ViewOffset;
            v.ViewedLeafCount = o.ViewedLeafCount;
            v.Year = o.Year;
            return v;
        }


    }
}
