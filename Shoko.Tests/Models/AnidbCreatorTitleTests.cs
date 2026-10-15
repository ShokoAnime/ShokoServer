using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers the titles of an AniDB creator or character: its transcribed name is its main title in <c>x-jat</c>
/// and its original name its official title in <c>ja</c>, the languages AniDB gives them in.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbCreatorTitleTests
{
    private static RepoFactoryScope Scope()
        => new RepoFactoryScope().Set(TestTextManager.Build(new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter())));

    [Fact]
    public void ACreatorIsTitledByItsTranscribedNameAndItsJapaneseOneByLanguage()
    {
        using var scope = Scope();
        var creator = (ICreator)new AniDB_Creator { CreatorID = 1, Name = "Hanazawa Kana", OriginalName = "花澤香菜" };

        Assert.Equal(["x-jat", "ja"], creator.Titles.Select(title => title.LanguageCode));
        Assert.Equal("Hanazawa Kana", creator.DefaultTitle.Value);
        Assert.Equal("Hanazawa Kana", creator.Title);
        var order = new[] { MetadataSource.AniDB };
        Assert.Equal("花澤香菜", TextChooser.ChooseTitle(creator.Titles, new([TitleLanguage.Japanese], order, false, false))?.Value);
        Assert.Equal("Hanazawa Kana", TextChooser.ChooseTitle(creator.Titles, new([TitleLanguage.Romaji], order, false, false))?.Value);
        Assert.Equal(("ja", "花澤香菜"), (creator.OriginalLanguageCode, creator.OriginalTitle?.Value));
    }

    [Fact]
    public void ACharacterIsTitledAsACreatorIs()
    {
        using var scope = Scope();
        var character = (ICharacter)new AniDB_Character { CharacterID = 1, Name = "Frieren", OriginalName = "フリーレン" };

        Assert.Equal(["x-jat", "ja"], character.Titles.Select(title => title.LanguageCode));
        Assert.Equal("Frieren", character.DefaultTitle.Value);
        Assert.Equal("フリーレン", character.OriginalTitle?.Value);
    }

    [Fact]
    public void ACreatorWithOnlyAJapaneseNameIsCalledByIt()
    {
        using var scope = Scope();
        var creator = (IWithTitles)new AniDB_Creator { CreatorID = 2, OriginalName = "花澤香菜" };

        Assert.Equal("花澤香菜", Assert.Single(creator.Titles).Value);
        Assert.Equal("花澤香菜", creator.DefaultTitle.Value);
        Assert.Null(((IWithTitles)new AniDB_Creator { CreatorID = 3, Name = "Someone" }).OriginalTitle);
    }
}
