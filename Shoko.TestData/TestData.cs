using System.Reflection;
using Newtonsoft.Json;
using Shoko.Server.Models.AniDB;

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

    public static Lazy<IReadOnlyList<RegularAirDateCase>> AnidbRegularAirDates { get; } = new(() =>
    {
        const string ResourceName = "Shoko.TestData.AnidbRegularAirDates.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream!);
        var jsonString = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<RegularAirDateCase[]>(jsonString)!;
    });

    /// <summary>
    /// A fully populated <c>settings-server.json</c> as 5.0.0 wrote it, at
    /// settings version 9.
    /// </summary>
    public static Lazy<string> ServerSettingsV5 { get; } = new(() =>
    {
        const string ResourceName = "Shoko.TestData.settings-server.v5.0.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    });
}
