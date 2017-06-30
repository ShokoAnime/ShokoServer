using System;

using NLog;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;

namespace Shoko.Server
{
    public class RenameFileHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        

        public static IRenamer GetRenamer()
        {
            var script = RepoFactory.RenameScript.GetDefaultScript();
            if (script == null) return null;
            return new LegacyRenamer(script);
        }

        public static IRenamer GetRenamerWithFallback()
        {
            var script = RepoFactory.RenameScript.GetDefaultOrFirst();
            if (script == null) return null;
            return new LegacyRenamer(script);
        }

        public static IRenamer GetRenamer(string scriptName)
        {
            var script = RepoFactory.RenameScript.GetByName(scriptName);
            if (script == null) return null;
            return new LegacyRenamer(script);
        }

        public static string GetNewFileName(SVR_VideoLocal_Place vid)
        {
            try
            {
                return GetRenamer()?.GetFileName(vid);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return string.Empty;
            }
        }
    }
}