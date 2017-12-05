using System.Collections.Generic;
using Shoko.Models.Enums;

namespace Shoko.Commons.Utils
{
    public class GroupFilterSortingCriteria
    {
        public int GroupFilterID { get; set; }

        public GroupFilterSorting SortType { get; set; } = GroupFilterSorting.AniDBRating;

        public GroupFilterSortDirection SortDirection { get; set; } = GroupFilterSortDirection.Asc;

        public static List<GroupFilterSortingCriteria> Create(int GroupFilterID, string value)
        {
            List<GroupFilterSortingCriteria> ls = new List<GroupFilterSortingCriteria>();
            if (!string.IsNullOrEmpty(value))
            {
                string[] scrit = value.Split('|');
                foreach (string sortpair in scrit)
                {
                    string[] spair = sortpair.Split(';');
                    if (spair.Length != 2) continue;

                    int stype;
                    int sdir;

                    int.TryParse(spair[0], out stype);
                    int.TryParse(spair[1], out sdir);

                    if (stype > 0 && sdir > 0)
                    {
                        GroupFilterSortingCriteria gfsc = new GroupFilterSortingCriteria
                        {
                            GroupFilterID = GroupFilterID,
                            SortType = (GroupFilterSorting)stype,
                            SortDirection = (GroupFilterSortDirection)sdir
                        };
                        ls.Add(gfsc);
                    }
                }
            }
            else
            {
                GroupFilterSortingCriteria gfsc = new GroupFilterSortingCriteria
                {
                    GroupFilterID = GroupFilterID,
                    SortType = GroupFilterSorting.GroupName,
                    SortDirection = GroupFilterSortDirection.Asc,
                };
            }
            return ls;
        }

       
    }
}