using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Server.Models;

namespace Shoko.Server.API
{
    /// <summary>
    /// This controller should be the base for every other controller. It has overrides to do anything before or after requests.
    /// An example is made for a request wide Random, solving the issue of a static Random somewhere/
    /// </summary>
    public class BaseController : Controller
    {
        // Override Controller.User to be the SVR_JMMUser, since we'll almost never need HttpContext.User
        protected new SVR_JMMUser User => HttpContext.GetUser();
        
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            HttpContext.Items.Add("Random", new Random());
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            base.OnActionExecuted(context);
            if (HttpContext.Request.Path.HasValue && !HttpContext.Request.Path.Value.StartsWith("/webui") &&
                !HttpContext.Request.Path.Value.StartsWith("/api/init/"))
            {
                AddConnection(HttpContext);
            }
        }

        public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            RemoveConnection(HttpContext);
            return base.OnActionExecutionAsync(context, next);
        }
        
        /// <summary>
        ///  A list of open connections to the API
        /// </summary>
        private static HashSet<string> _openConnections = new HashSet<string>();
        /// <summary>
        /// blur the connection state to 5s, as most media players and calls are spread.
        /// This prevents flickering of the state for UI
        /// </summary>
        private static Timer _connectionTimer;

        private static void AddConnection(HttpContext ctx)
        {
            
            ctx.Items["ContextGUID"] = ctx.Connection.Id;
            lock (_openConnections)
            {
                _openConnections.Add(ctx.Connection.Id);
                ServerState.Instance.ApiInUse = _openConnections.Count > 0;
            }
        }
        
        private static void RemoveConnection(HttpContext ctx)
        {
            if (!ctx.Items.ContainsKey("ContextGUID")) return;
            lock (_openConnections)
            {
                _openConnections.Remove((string) ctx.Items["ContextGUID"]);
            }
            ResetTimer();
        }

        private static void ResetTimer()
        {
            lock (_connectionTimer)
            {
                DisposeTimer();
                CreateTimer();
                _connectionTimer.Start();
            }
        }

        /// <summary>
        /// Destroy the timer and unsubscribe events, etc.
        /// </summary>
        private static void DisposeTimer()
        {
            if (_connectionTimer == null) return;
            _connectionTimer.Stop();
            _connectionTimer.Elapsed -= TimerElapsed;
            _connectionTimer.Dispose();
        }

        /// <summary>
        /// Create and start the timer
        /// </summary>
        private static void CreateTimer()
        {
            _connectionTimer = new Timer { Interval = 5000 };
            _connectionTimer.Elapsed += TimerElapsed;
        }

        private static void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (_openConnections)
            {
                ServerState.Instance.ApiInUse = _openConnections.Count > 0;
            }
        }
    }
}