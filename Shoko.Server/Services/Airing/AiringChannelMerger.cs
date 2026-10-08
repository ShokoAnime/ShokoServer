using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services.Airing;

/// <summary>
/// Folds channels into another one, and re-keys one: the rows and settings
/// side of <see cref="AiringScheduleService.MergeChannels"/>, shared with the
/// database fix that keys the stored channels by country, and gives a channel
/// without a country the one it is found in. It raises no events.
/// </summary>
internal static class AiringChannelMerger
{
    #region Merging

    /// <summary>
    /// Moves everything that names the sources over to the target, adds their
    /// names to its aliases and deletes them. Schedule keys are left as they
    /// are, so no schedule or airing ID changes.
    /// </summary>
    /// <param name="target">The channel to keep.</param>
    /// <param name="sources">The channels to merge into it, none of them the target.</param>
    /// <param name="configurationProvider">The service's settings, holding the channel preferences.</param>
    /// <param name="logger">Told about the names left out of the aliases.</param>
    /// <returns>The schedules that moved to the target.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public static IReadOnlyList<AiringSchedule> Merge(
        AiringChannel target,
        IReadOnlyList<AiringChannel> sources,
        ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider,
        ILogger logger
    )
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(configurationProvider);
        ArgumentNullException.ThrowIfNull(logger);

        if (sources.Count is 0)
            return [];

        var moved = new List<AiringSchedule>();
        foreach (var source in sources)
        {
            foreach (var schedule in RepoFactory.AiringSchedule.GetByChannelID(source.ChannelID))
            {
                schedule.ChannelID = target.ChannelID;
                RepoFactory.AiringSchedule.Save(schedule);
                moved.Add(schedule);
            }

            MoveImages(((IMetadata)source).ID, ((IMetadata)target).ID);
        }

        FoldSettings(target.ChannelID, sources.Select(source => source.ChannelID).ToHashSet(), configurationProvider);

        var names = sources
            .SelectMany(source => source.Aliases.Prepend(source.Name))
            .ToList();
        var sourceIDs = sources.Select(source => source.AiringChannelID).ToHashSet();
        foreach (var source in sources)
            RepoFactory.AiringChannel.Delete(source);

        target.Aliases = [.. target.Aliases, .. GetNewAliases(target, names, sourceIDs, logger)];
        RepoFactory.AiringChannel.Save(target);
        return moved;
    }

    /// <summary>
    /// Picks the names a merge adds to the target's aliases: each once, never
    /// its own name or an alias it has, and never a name another channel of
    /// its type and country answers to.
    /// </summary>
    /// <param name="target">The channel the names are added to.</param>
    /// <param name="names">The names of the merged channels, own names and aliases.</param>
    /// <param name="ignoredChannelIDs">The local IDs of the merged channels, which may still be cached.</param>
    /// <param name="logger">Told about the names left out.</param>
    /// <returns>The names to add.</returns>
    private static List<string> GetNewAliases(AiringChannel target, IEnumerable<string> names, IReadOnlySet<int> ignoredChannelIDs, ILogger logger)
    {
        var known = target.NormalizedNames.ToHashSet(StringComparer.Ordinal);
        var added = new List<string>();
        foreach (var name in names)
        {
            var normalizedName = AiringScheduleUtility.NormalizeChannelName(name);
            if (normalizedName.Length is 0 || !known.Add(normalizedName))
                continue;

            var holder = RepoFactory.AiringChannel.GetAllByName(name, target.Type, target.CountryCode)
                .FirstOrDefault(other => other.AiringChannelID != target.AiringChannelID && !ignoredChannelIDs.Contains(other.AiringChannelID));
            if (holder is not null)
            {
                logger.LogWarning(
                    "Merging into channel \"{Name}\" leaves out the alias \"{Alias}\", which channel \"{OtherName}\" already answers to.",
                    target.Name,
                    name.Trim(),
                    holder.Name
                );
                continue;
            }

            added.Add(name.Trim());
        }

        return added;
    }

    /// <summary>
    /// Folds the merged channels into the target in the preferred channels:
    /// the target takes the best position any of them had.
    /// </summary>
    /// <param name="targetID">The ID of the channel kept.</param>
    /// <param name="sourceIDs">The IDs of the channels merged into it.</param>
    /// <param name="configurationProvider">The service's settings.</param>
    private static void FoldSettings(Guid targetID, IReadOnlySet<Guid> sourceIDs, ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider)
    {
        var settings = configurationProvider.Load();
        var preferred = new List<Guid>();
        var placed = false;
        foreach (var channelID in settings.PreferredChannels)
        {
            if (channelID == targetID || sourceIDs.Contains(channelID))
            {
                if (!placed)
                    preferred.Add(targetID);
                placed = true;
                continue;
            }

            preferred.Add(channelID);
        }

        if (preferred.SequenceEqual(settings.PreferredChannels))
            return;

        settings.PreferredChannels = preferred;
        configurationProvider.Save(settings);
    }

    /// <summary>
    /// Moves the images linked to one channel ID over to another. An image the
    /// other already has stays its own, and its preferred image of a type stays
    /// preferred.
    /// </summary>
    /// <param name="sourceID">The entity ID the images are linked to now.</param>
    /// <param name="targetID">The entity ID to link them to.</param>
    private static void MoveImages(MetadataGuid sourceID, MetadataGuid targetID)
    {
        var links = RepoFactory.ShokoImage_Entity.GetByEntity(sourceID);
        if (links.Count is 0)
            return;

        var targetLinks = RepoFactory.ShokoImage_Entity.GetByEntity(targetID);
        var held = targetLinks.Select(link => (link.ImageID, link.ImageType)).ToHashSet();
        var preferred = targetLinks.Where(link => link.IsPreferred).Select(link => link.ImageType).ToHashSet();
        var duplicates = new List<ShokoImage_Entity>();
        foreach (var link in links)
        {
            if (!held.Add((link.ImageID, link.ImageType)))
            {
                duplicates.Add(link);
                continue;
            }

            link.EntitySource = targetID.Source;
            link.EntityType = targetID.EntityType;
            link.EntityID = targetID.ID;
            if (link.IsPreferred && !preferred.Add(link.ImageType))
                link.IsPreferred = false;
            RepoFactory.ShokoImage_Entity.Save(link);
        }

        if (duplicates.Count > 0)
            RepoFactory.ShokoImage_Entity.Delete(duplicates);
    }

    #endregion

    #region Re-keying

    /// <summary>
    /// Gives a channel a new name and country, and with them a new ID, and
    /// moves everything that named the old ID over to the new one. Schedule
    /// keys are left as they are, so no schedule or airing ID changes.
    /// </summary>
    /// <param name="channel">The channel to re-key.</param>
    /// <param name="name">The new display name.</param>
    /// <param name="countryCode">The new country, or <c>null</c> for none.</param>
    /// <param name="configurationProvider">The service's settings, holding the channel preferences.</param>
    /// <returns>The schedules that moved to the new ID.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank, or <paramref name="countryCode"/> is not two letters.</exception>
    public static IReadOnlyList<AiringSchedule> Rekey(
        AiringChannel channel,
        string name,
        string? countryCode,
        ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider
    )
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(configurationProvider);

        var oldID = channel.ChannelID;
        var oldEntityID = ((IMetadata)channel).ID;
        channel.SetKey(name, countryCode);
        RepoFactory.AiringChannel.Save(channel);
        if (channel.ChannelID == oldID)
            return [];

        var moved = new List<AiringSchedule>();
        foreach (var schedule in RepoFactory.AiringSchedule.GetByChannelID(oldID))
        {
            schedule.ChannelID = channel.ChannelID;
            RepoFactory.AiringSchedule.Save(schedule);
            moved.Add(schedule);
        }

        MoveImages(oldEntityID, ((IMetadata)channel).ID);

        var settings = configurationProvider.Load();
        if (!settings.PreferredChannels.Contains(oldID))
            return moved;

        settings.PreferredChannels = settings.PreferredChannels
            .Select(channelID => channelID == oldID ? channel.ChannelID : channelID)
            .Distinct()
            .ToList();
        configurationProvider.Save(settings);
        return moved;
    }

    #endregion

    #region Countries

    /// <summary>
    /// Checks whether a channel may take a country it is found in. Only a TV
    /// station without a country may: its country is unknown, while a
    /// streaming service without one is global.
    /// </summary>
    /// <param name="channel">The stored channel.</param>
    /// <returns><c>true</c> if the channel may take a country.</returns>
    public static bool CanTakeCountry(AiringChannel channel)
        => channel is { Type: AiringChannelType.Television, CountryCode: null };

    /// <summary>
    /// Gives a channel without a country the given one. When a channel of its
    /// type in that country already answers to its name, the channel is merged
    /// into that one instead; otherwise it is re-keyed in place, without the
    /// aliases another channel there answers to.
    /// </summary>
    /// <param name="channel">The channel to give the country, without one.</param>
    /// <param name="countryCode">The normalised country to give it.</param>
    /// <param name="configurationProvider">The service's settings, holding the channel preferences.</param>
    /// <param name="logger">Told about the aliases left out.</param>
    /// <returns>
    /// The channel now holding it, the schedules that moved, and the aliases
    /// the channel now holding it had before.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="countryCode"/> is not two letters.</exception>
    public static (AiringChannel Channel, IReadOnlyList<AiringSchedule> Moved, IReadOnlyList<string> PreviousAliases) AdoptCountry(
        AiringChannel channel,
        string countryCode,
        ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider,
        ILogger logger
    )
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(countryCode);
        ArgumentNullException.ThrowIfNull(configurationProvider);
        ArgumentNullException.ThrowIfNull(logger);

        if (RepoFactory.AiringChannel.GetByName(channel.Name, channel.Type, countryCode) is { } holder && holder.AiringChannelID != channel.AiringChannelID)
        {
            var holderAliases = holder.Aliases.ToList();
            return (holder, Merge(holder, [channel], configurationProvider, logger), holderAliases);
        }

        var kept = new List<string>();
        foreach (var alias in channel.Aliases)
        {
            var other = RepoFactory.AiringChannel.GetAllByName(alias, channel.Type, countryCode)
                .FirstOrDefault(other => other.AiringChannelID != channel.AiringChannelID);
            if (other is null)
            {
                kept.Add(alias);
                continue;
            }

            logger.LogWarning(
                "Channel \"{Name}\" drops the alias \"{Alias}\" on taking country {CountryCode}, channel \"{OtherName}\" already answers to it.",
                channel.Name,
                alias,
                countryCode,
                other.Name
            );
        }

        var previousAliases = channel.Aliases.ToList();
        channel.Aliases = kept;
        return (channel, Rekey(channel, channel.Name, countryCode, configurationProvider), previousAliases);
    }

    #endregion
}
