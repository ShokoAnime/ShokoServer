using System.Reflection;
using Newtonsoft.Json;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;

namespace Shoko.TestData;

public static class TestData
{
    public static Lazy<IEnumerable<AniDB_Anime>> AniDB_Anime { get; } = new(() =>
    {
        const string ResourceName = "Shoko.TestData.AniDB_Anime.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream!);
        var jsonString = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<AniDB_Anime[]>(jsonString)!;
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
