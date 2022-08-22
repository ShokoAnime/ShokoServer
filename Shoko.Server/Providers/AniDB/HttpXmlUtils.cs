using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB
{
    public class HttpXmlUtils
    {
        private readonly ILogger<HttpXmlUtils> _logger;
        private readonly ServerSettings _settings;

        public HttpXmlUtils(ILogger<HttpXmlUtils> logger, ServerSettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        public string LoadAnimeHTTPFromFile(int animeID)
        {
            var filePath = _settings.AnimeXmlDirectory;
            var fileName = $"AnimeDoc_{animeID}.xml";
            var fileNameWithPath = Path.Combine(filePath, fileName);

            _logger.LogTrace("Trying to load anime XML from cache: {FileNameWithPath}", fileNameWithPath);
            if (!Directory.Exists(filePath))
            {
                _logger.LogTrace("XML cache directory does not exist. Trying to create it: {FilePath}", filePath);
                Directory.CreateDirectory(filePath);
            }

            if (!File.Exists(fileNameWithPath))
            {
                _logger.LogTrace("XML file {FileNameWithPath} does not exist. exiting", fileNameWithPath);
                return null;
            }

            using var re = File.OpenText(fileNameWithPath);
            _logger.LogTrace("File exists. Loading anime XML from cache: {FileNameWithPath}", fileNameWithPath);
            var rawXml = re.ReadToEnd();

            return rawXml;
        }

        public void WriteAnimeHTTPToFile(int animeID, string xml)
        {
            try
            {
                var filePath = _settings.AnimeXmlDirectory;
                var fileName = $"AnimeDoc_{animeID}.xml";
                var fileNameWithPath = Path.Combine(filePath, fileName);

                _logger.LogTrace("Writing anime XML to cache: {FileNameWithPath}", fileNameWithPath);
                if (!Directory.Exists(filePath))
                {
                    _logger.LogTrace("XML cache directory does not exist. Trying to create it: {FilePath}", filePath);
                    Directory.CreateDirectory(filePath);
                }

                // First check to make sure we not rights issue
                if (!Utils.IsDirectoryWritable(filePath))
                {
                    _logger.LogTrace("Unable to access {FileNameWithPath}. Insufficient permissions. Attempting to grant", fileNameWithPath);
                    return;
                }

                // Check again and only if write-able we create it
                _logger.LogTrace("Can write to {FilePath}. Writing xml file {FileNameWithPath}", filePath, fileNameWithPath);
                using var sw = File.CreateText(fileNameWithPath);
                sw.Write(xml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during WriteAnimeHTTPToFile(): {Ex}", ex);
            }
        }
    }
}