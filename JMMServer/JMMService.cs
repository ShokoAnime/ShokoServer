using System;
using JMMServer.Commands;
using JMMServer.Databases;
using JMMServer.Providers.TvDB;
using NHibernate;
using NLog;

namespace JMMServer
{
    public class JMMService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly object sessionLock = new object();
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
                    return JMMService.lastAniDBMessage;
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
                    return JMMService.lastAniDBUDPMessage;
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
                    return JMMService.lastAniDBHTTPMessage;
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
                    return JMMService.lastAniDBMessageNonPing;
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
                    return JMMService.lastAniDBPing;
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
                    return JMMService.cmdProcessorGeneral;
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
                    return JMMService.cmdProcessorImages;
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
                    return JMMService.cmdProcessorHasher;
                }
            }
        }

        private static AniDBHelper anidbProcessor = new AniDBHelper();

        public static AniDBHelper AnidbProcessor
        {
            get { return JMMService.anidbProcessor; }
        }

        private static TvDBHelper tvdbHelper = new TvDBHelper();

        public static TvDBHelper TvdbHelper
        {
            get { return JMMService.tvdbHelper; }
        }

        private static ISessionFactory sessionFactory = null;

        public static ISessionFactory SessionFactory
        {
            get
            {
                lock (sessionLock)
                {
                    if (sessionFactory == null)
                    {
                        //logger.Info("Creating new session...");
                        sessionFactory = DatabaseHelper.CreateSessionFactory();
                    }
                    return JMMService.sessionFactory;
                }
            }
        }

        public static void CloseSessionFactory()
        {
            if (sessionFactory != null) sessionFactory.Dispose();
            sessionFactory = null;
        }
    }
}