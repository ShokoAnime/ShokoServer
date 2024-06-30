using System.Reflection;
using Newtonsoft.Json;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.TestData;

public static class TestData
{
    public static Lazy<IEnumerable<SVR_AniDB_Anime>> AniDB_Anime { get; } = new(() =>
    {
        const string ResourceName = "Shoko.TestData.AniDB_Anime.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream!);
        var jsonString = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<SVR_AniDB_Anime[]>(jsonString)!;
    });

    public static Lazy<IEnumerable<SVR_AniDB_File>> AniDB_File { get; } = new(() =>
    {
        const string ResourceName = "Shoko.TestData.AniDB_File.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream!);
        var jsonString = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<SVR_AniDB_File[]>(jsonString)!;
    });

    public static Lazy<IEnumerable<CrossRef_File_Episode>> CrossRef_File_Episode { get; } = new(() =>
    {
        const string ResourceName = "Shoko.TestData.AniDB_Anime.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream!);
        var jsonString = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<CrossRef_File_Episode[]>(jsonString)!;
    });
}
