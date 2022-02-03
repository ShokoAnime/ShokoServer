using System;
using System.Collections.Generic;
using System.Timers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Server.Server;

namespace Shoko.Server.API.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class ApiInUseAttribute : Attribute, IAlwaysRunResultFilter
    {
        static ApiInUseAttribute()
        {
            ConnectionTimer.Elapsed += TimerElapsed;
        }
        
        /// <summary>
        ///  A list of open connections to the API
        /// </summary>
        private static readonly HashSet<string> OpenConnections = new HashSet<string>();
        /// <summary>
        /// blur the connection state to 5s, as most media players and calls are spread.
        /// This prevents flickering of the state for UI
        /// </summary>
        private static readonly Timer ConnectionTimer = new Timer(5000);
        
        private static void AddConnection(HttpContext ctx)
        {
            lock (OpenConnections)
            {
                OpenConnections.Add(ctx.Connection.Id);
                ServerState.Instance.ApiInUse = OpenConnections.Count > 0;
            }
        }
        
        private static void RemoveConnection(HttpContext ctx)
        {
            lock (OpenConnections)
            {
                OpenConnections.Remove(ctx.Connection.Id);
            }
            ResetTimer();
        }

        private static void ResetTimer()
        {
            lock (ConnectionTimer)
            {
                ConnectionTimer.Stop();
                ConnectionTimer.Start();
            }
        }

        private static void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (OpenConnections)
            {
                ServerState.Instance.ApiInUse = OpenConnections.Count > 0;
            }
        }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            AddConnection(context.HttpContext);
        }
        
        public void OnResultExecuted(ResultExecutedContext context)
        {
            RemoveConnection(context.HttpContext);
        }
    }
}