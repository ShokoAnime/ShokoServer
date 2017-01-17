using System;
using Shoko.Server.Databases;
using NHibernate;
using NLog;
using Shoko.Server.Commands;
using Shoko.Models.TvDB;

namespace Shoko.Server
{
    public class ShokoService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static readonly object cmdLockGeneral = new object();
        private static readonly object cmdLockHasher = new object();
        private static readonly object cmdLockImages = new object();
        private static readonly object lockLastAniDBMessage = new object();
        private static readonly object lockLastAniDBUDPMessage = new object();
        private static readonly object lockLastAniDBHTTPMessage = new object();
        private static readonly object lockLastAniDBMessageNonPing = new object();
        private static readonly object lockLastAniDBPing = new object();

        public static bool DebugFlag = false;

        public static void LogToSystem(string logType, string logMessage)
        {
            logger.Info(string.Format("{0} - {1}", logType, logMessage));
        }

        private static DateTime lastAniDBMessage = DateTime.Now;

        public static DateTime LastAniDBMessage
        {
            get
            {
                lock (lockLastAniDBMessage)
                {
                    return ShokoService.lastAniDBMessage;
                }
            }
            set { lastAniDBMessage = value; }
        }

        private static DateTime lastAniDBUDPMessage = DateTime.Now;

        public static DateTime LastAniDBUDPMessage
        {
            get
            {
                lock (lockLastAniDBUDPMessage)
                {
                    return ShokoService.lastAniDBUDPMessage;
                }
            }
            set { lastAniDBUDPMessage = value; }
        }

        private static DateTime lastAniDBHTTPMessage = DateTime.Now;

        public static DateTime LastAniDBHTTPMessage
        {
            get
            {
                lock (lockLastAniDBHTTPMessage)
                {
                    return ShokoService.lastAniDBHTTPMessage;
                }
            }
            set { lastAniDBHTTPMessage = value; }
        }


        private static DateTime lastAniDBMessageNonPing = DateTime.Now;

        public static DateTime LastAniDBMessageNonPing
        {
            get
            {
                lock (lockLastAniDBMessageNonPing)
                {
                    return ShokoService.lastAniDBMessageNonPing;
                }
            }
            set { lastAniDBMessageNonPing = value; }
        }

        private static DateTime lastAniDBPing = DateTime.Now;

        public static DateTime LastAniDBPing
        {
            get
            {
                lock (lockLastAniDBPing)
                {
                    return ShokoService.lastAniDBPing;
                }
            }
            set { lastAniDBPing = value; }
        }

        private static CommandProcessorGeneral cmdProcessorGeneral = new CommandProcessorGeneral();

        public static CommandProcessorGeneral CmdProcessorGeneral
        {
            get
            {
                lock (cmdLockGeneral)
                {
                    return ShokoService.cmdProcessorGeneral;
                }
            }
        }

        private static CommandProcessorImages cmdProcessorImages = new CommandProcessorImages();

        public static CommandProcessorImages CmdProcessorImages
        {
            get
            {
                lock (cmdLockImages)
                {
                    return ShokoService.cmdProcessorImages;
                }
            }
        }

        private static CommandProcessorHasher cmdProcessorHasher = new CommandProcessorHasher();

        public static CommandProcessorHasher CmdProcessorHasher
        {
            get
            {
                lock (cmdLockHasher)
                {
                    return ShokoService.cmdProcessorHasher;
                }
            }
        }

        private static AniDBHelper anidbProcessor = new AniDBHelper();

        public static AniDBHelper AnidbProcessor
        {
            get { return ShokoService.anidbProcessor; }
        }

        private static TvDBHelper tvdbHelper = new TvDBHelper();

        public static TvDBHelper TvdbHelper
        {
            get { return ShokoService.tvdbHelper; }
        }
    }
}