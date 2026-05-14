using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Providers.AniDB;

public class HttpXmlUtils(ILogger<HttpXmlUtils> logger, IApplicationPaths applicationPaths)
{

    public string AnimeXmlDirectory => Path.Combine(applicationPaths.DataPath, "Anime_HTTP");

    public async Task<string> LoadAnimeHTTPFromFile(int animeID)
    {
        var filePath = AnimeXmlDirectory;
        var fileName = $"AnimeDoc_{animeID}.xml";
        var fileNameWithPath = Path.Combine(filePath, fileName);

        logger.LogTrace("Trying to load anime XML from cache: {FileNameWithPath}", fileNameWithPath);
        if (!Directory.Exists(filePath))
        {
            logger.LogTrace("XML cache directory does not exist. Trying to create it: {FilePath}", filePath);
            Directory.CreateDirectory(filePath);
        }

        if (!File.Exists(fileNameWithPath))
        {
            logger.LogTrace("XML file {FileNameWithPath} does not exist. exiting", fileNameWithPath);
            return null;
        }

        using var re = File.OpenText(fileNameWithPath);
        logger.LogTrace("File exists. Loading anime XML from cache: {FileNameWithPath}", fileNameWithPath);
        var rawXml = await re.ReadToEndAsync();

        return rawXml;
    }

    public async Task WriteAnimeHTTPToFile(int animeID, string xml)
    {
        try
        {
            var filePath = AnimeXmlDirectory;
            var fileName = $"AnimeDoc_{animeID}.xml";
            var fileNameWithPath = Path.Combine(filePath, fileName);

            logger.LogTrace("Writing anime XML to cache: {FileNameWithPath}", fileNameWithPath);
            if (!Directory.Exists(filePath))
            {
                logger.LogTrace("XML cache directory does not exist. Trying to create it: {FilePath}", filePath);
                Directory.CreateDirectory(filePath);
            }

            // Check again and only if write-able we create it
            logger.LogTrace("Attempting to write xml file {FileNameWithPath}", fileNameWithPath);
            await using var sw = File.CreateText(fileNameWithPath);
            await sw.WriteAsync(xml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during WriteAnimeHTTPToFile()");
        }
    }
}
