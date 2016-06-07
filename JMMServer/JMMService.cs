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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
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

        private static DateTime lastAniDBMessage = DateTime.Now;

        private static DateTime lastAniDBUDPMessage = DateTime.Now;

        private static DateTime lastAniDBHTTPMessage = DateTime.Now;


        private static DateTime lastAniDBMessageNonPing = DateTime.Now;

        private static DateTime lastAniDBPing = DateTime.Now;

        private static readonly CommandProcessorGeneral cmdProcessorGeneral = new CommandProcessorGeneral();

        private static readonly CommandProcessorImages cmdProcessorImages = new CommandProcessorImages();

        private static readonly CommandProcessorHasher cmdProcessorHasher = new CommandProcessorHasher();

        private static ISessionFactory sessionFactory;

        public static DateTime LastAniDBMessage
        {
            get
            {
                lock (lockLastAniDBMessage)
                {
                    return lastAniDBMessage;
                }
            }
            set { lastAniDBMessage = value; }
        }

        public static DateTime LastAniDBUDPMessage
        {
            get
            {
                lock (lockLastAniDBUDPMessage)
                {
                    return lastAniDBUDPMessage;
                }
            }
            set { lastAniDBUDPMessage = value; }
        }

        public static DateTime LastAniDBHTTPMessage
        {
            get
            {
                lock (lockLastAniDBHTTPMessage)
                {
                    return lastAniDBHTTPMessage;
                }
            }
            set { lastAniDBHTTPMessage = value; }
        }

        public static DateTime LastAniDBMessageNonPing
        {
            get
            {
                lock (lockLastAniDBMessageNonPing)
                {
                    return lastAniDBMessageNonPing;
                }
            }
            set { lastAniDBMessageNonPing = value; }
        }

        public static DateTime LastAniDBPing
        {
            get
            {
                lock (lockLastAniDBPing)
                {
                    return lastAniDBPing;
                }
            }
            set { lastAniDBPing = value; }
        }

        public static CommandProcessorGeneral CmdProcessorGeneral
        {
            get
            {
                lock (cmdLockGeneral)
                {
                    return cmdProcessorGeneral;
                }
            }
        }

        public static CommandProcessorImages CmdProcessorImages
        {
            get
            {
                lock (cmdLockImages)
                {
                    return cmdProcessorImages;
                }
            }
        }

        public static CommandProcessorHasher CmdProcessorHasher
        {
            get
            {
                lock (cmdLockHasher)
                {
                    return cmdProcessorHasher;
                }
            }
        }

        public static AniDBHelper AnidbProcessor { get; } = new AniDBHelper();

        public static TvDBHelper TvdbHelper { get; } = new TvDBHelper();

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
                    return sessionFactory;
                }
            }
        }

        public static void LogToSystem(string logType, string logMessage)
        {
            logger.Info(string.Format("{0} - {1}", logType, logMessage));
        }

        public static void CloseSessionFactory()
        {
            if (sessionFactory != null) sessionFactory.Dispose();
            sessionFactory = null;
        }
    }
}