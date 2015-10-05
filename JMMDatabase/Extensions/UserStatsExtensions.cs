using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMDatabase.Extensions
{
    public static class UserStatsExtensions
    {
        public static void SetWatchedState<T>(this List<T> ws, bool watched, string userid, DateTime? watchedDate, bool updateWatchedDate) where T:UserStats, new()
        {
            if (watched)
            {
                T us = ws.FirstOrDefault(a => a.JMMUserId == userid);
                if (us == null)
                {
                    us = new T();
                    us.JMMUserId = userid;
                    us.WatchedDate = DateTime.Now;
                }
                us.WatchedCount++;
                if (updateWatchedDate && watchedDate.HasValue)
                    us.WatchedDate = watchedDate.Value;
            }
            else
            {
                T us = ws.FirstOrDefault(a => a.JMMUserId == userid);
                if (us != null)
                {
                    ws.Remove(us);
                }
            }
        }
    }
}
