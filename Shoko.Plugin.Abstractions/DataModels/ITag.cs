namespace Shoko.Plugin.Abstractions.DataModels;

public interface ITag
{
    string Name { get; }
    string Description { get; }
    bool Spoiler { get; }
}
