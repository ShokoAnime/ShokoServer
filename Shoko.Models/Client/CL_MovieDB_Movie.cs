﻿using System;

namespace Shoko.Models.Client
{
    public class CL_MovieDB_Movie : ICloneable
    {
        public int MovieDB_MovieID { get; set; }
        public int MovieId { get; set; }
        public string MovieName { get; set; }
        public string OriginalName { get; set; }
        public string Overview { get; set; }
        public int Rating { get; set; } // saved at * 10 to preserve decimal. resulting in 82/100

        public object Clone()
        {
            return new CL_MovieDB_Movie
            {
                MovieDB_MovieID = MovieDB_MovieID,
                MovieId = MovieId,
                MovieName = MovieName,
                OriginalName = OriginalName,
                Overview = Overview,
                Rating = Rating
            };
        }
    }
}
