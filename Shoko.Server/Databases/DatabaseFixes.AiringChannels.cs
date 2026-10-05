using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Airing Channels | Fields

    /// <summary>
    /// The global streaming services our providers used to register with the
    /// country they were watched from, e.g. <c>Netflix (JP)</c>. They are one
    /// channel worldwide, so they lose the country. Normalised names.
    /// </summary>
    internal static readonly IReadOnlySet<string> GlobalStreamingBrands = new HashSet<string>(StringComparer.Ordinal)
    {
        "amazon",
        "amazon prime video",
        "apple tv",
        "apple tv+",
        "crunchyroll",
        "disney plus",
        "disney+",
        "hidive",
        "netflix",
        "prime video",
        "youtube",
    };

    /// <summary>
    /// A name ending in a country in parentheses, e.g. <c>Tokyo MX (JP)</c>.
    /// </summary>
    private static readonly Regex _regionalChannelName = new(@"^(?<name>.*\S)\s*\((?<country>[A-Z]{2})\)$", RegexOptions.Compiled);

    #endregion

    #region Airing Channels | Steps

    /// <summary>
    /// Moves the country of every stored channel from its name to its own
    /// column, gives each the ID of its new key, and merges the channels that
    /// now share a key.
    /// </summary>
    public static void KeyAiringChannelsByCountry()
    {
        var services = ISystemService.StaticServices;
        KeyAiringChannelsByCountry(
            services.GetRequiredService<ConfigurationProvider<AiringScheduleServiceSettings>>(),
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseFixes))
        );
    }

    /// <summary>
    /// Keys the stored channels by country, as
    /// <see cref="KeyAiringChannelsByCountry()"/> does.
    /// </summary>
    /// <remarks>
    /// A trailing <c>(XX)</c> becomes the country, except on a global streaming
    /// service, which loses it. A TV station without a country takes the one
    /// country its namesakes have. Everything naming a channel follows it to
    /// its new ID, and the channels sharing a key are merged into the one
    /// already holding it, else the oldest.
    /// </remarks>
    /// <param name="configurationProvider">The airing service's settings, holding the channel preferences.</param>
    /// <param name="logger">Told what was changed.</param>
    /// <returns>How many channels were re-keyed or merged away.</returns>
    internal static int KeyAiringChannelsByCountry(ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider, ILogger logger)
    {
        var channels = RepoFactory.AiringChannel.GetAll()
            .OrderBy(channel => channel.AiringChannelID)
            .ToList();
        var plans = channels
            .Select(channel => (Channel: channel, Key: GetCountryKey(channel)))
            .ToList();

        // A TV station is in one country: one without a country is the same
        // station as its namesakes when they all name the same one.
        for (var index = 0; index < plans.Count; index++)
        {
            var (channel, key) = plans[index];
            if (channel.Type is not AiringChannelType.Television || key.CountryCode is not null)
                continue;

            var normalizedName = AiringScheduleUtility.NormalizeChannelName(key.Name);
            var countries = plans
                .Where(plan => plan.Channel.Type is AiringChannelType.Television && plan.Key.CountryCode is not null)
                .Where(plan => AiringScheduleUtility.NormalizeChannelName(plan.Key.Name) == normalizedName)
                .Select(plan => plan.Key.CountryCode)
                .Distinct()
                .ToList();
            if (countries.Count is 1)
                plans[index] = (channel, (key.Name, countries[0]));
        }

        var changed = 0;
        foreach (var group in plans.GroupBy(plan => AiringScheduleUtility.GetChannelID(plan.Key.Name, plan.Channel.Type, plan.Key.CountryCode)))
        {
            var members = group.ToList();
            var (target, targetKey) = members.FirstOrDefault(plan => plan.Channel.ChannelID == group.Key) is { Channel: not null } holder
                ? holder
                : members[0];
            if (target.ChannelID != group.Key || target.Name != targetKey.Name || target.CountryCode != targetKey.CountryCode)
            {
                logger.LogInformation(
                    "Keying channel \"{OldName}\" as \"{Name}\" in country {CountryCode}.",
                    target.Name,
                    targetKey.Name,
                    targetKey.CountryCode ?? "(none)"
                );
                AiringChannelMerger.Rekey(target, targetKey.Name, targetKey.CountryCode, configurationProvider);
                changed++;
            }

            var sources = members
                .Where(plan => plan.Channel.AiringChannelID != target.AiringChannelID)
                .Select(plan =>
                {
                    // The name without its country is what becomes an alias.
                    plan.Channel.Name = plan.Key.Name;
                    return plan.Channel;
                })
                .ToList();
            if (sources.Count is 0)
                continue;

            logger.LogInformation(
                "Merging {Count} channel(s) into channel \"{Name}\" ({ChannelID}): {Names}.",
                sources.Count,
                target.Name,
                target.ChannelID,
                string.Join(", ", sources.Select(source => source.Name))
            );
            AiringChannelMerger.Merge(target, sources, configurationProvider, logger);
            changed += sources.Count;
        }

        changed += RemoveConflictingAliases(logger);
        return changed;
    }

    #endregion

    #region Airing Channels | Helpers

    /// <summary>
    /// Splits a channel name ending in a country in parentheses, the way
    /// regional channels used to be named, e.g. <c>Tokyo MX (JP)</c>.
    /// </summary>
    /// <param name="name">The channel name.</param>
    /// <param name="brand">The name without the country.</param>
    /// <param name="countryCode">The country.</param>
    /// <returns><c>true</c> if the name ended in a country.</returns>
    internal static bool TrySplitRegionalChannelName(string name, out string brand, out string countryCode)
    {
        var match = _regionalChannelName.Match(name.Trim());
        brand = match.Success ? match.Groups["name"].Value : name;
        countryCode = match.Success ? match.Groups["country"].Value : string.Empty;
        return match.Success;
    }

    /// <summary>
    /// Works out the name and country a stored channel is keyed by.
    /// </summary>
    /// <param name="channel">The stored channel.</param>
    /// <returns>The name and the country, if any.</returns>
    private static (string Name, string? CountryCode) GetCountryKey(AiringChannel channel)
    {
        var (name, countryCode) = channel.CountryCode is not null
            ? (channel.Name, channel.CountryCode)
            : TrySplitRegionalChannelName(channel.Name, out var brand, out var country)
                ? (brand, country)
                : (channel.Name, (string?)null);
        if (channel.Type is AiringChannelType.Streaming && GlobalStreamingBrands.Contains(AiringScheduleUtility.NormalizeChannelName(name)))
            countryCode = null;

        return (name, countryCode);
    }

    /// <summary>
    /// Restores one name, one answer within each type and country after the
    /// channels changed country: an alias that is another channel's own name
    /// is dropped, and of two channels sharing an alias the older keeps it.
    /// </summary>
    /// <param name="logger">Told about the aliases dropped.</param>
    /// <returns>How many channels lost an alias.</returns>
    private static int RemoveConflictingAliases(ILogger logger)
    {
        var channels = RepoFactory.AiringChannel.GetAll()
            .OrderBy(channel => channel.AiringChannelID)
            .ToList();
        var ownNames = channels
            .Select(channel => (channel.Type, channel.CountryCode, channel.NormalizedName))
            .ToHashSet();
        var claimed = new HashSet<(AiringChannelType, string?, string)>();
        var changed = 0;
        foreach (var channel in channels)
        {
            var kept = new List<string>();
            foreach (var alias in channel.Aliases)
            {
                var normalizedAlias = AiringScheduleUtility.NormalizeChannelName(alias);
                if (normalizedAlias.Length is 0 || normalizedAlias == channel.NormalizedName)
                    continue;

                var name = (channel.Type, channel.CountryCode, normalizedAlias);
                if (ownNames.Contains(name) || !claimed.Add(name))
                {
                    logger.LogInformation("Dropping the alias \"{Alias}\" of channel \"{Name}\", another channel answers to it.", alias, channel.Name);
                    continue;
                }

                kept.Add(alias);
            }

            if (kept.Count == channel.Aliases.Count)
                continue;

            channel.Aliases = kept;
            RepoFactory.AiringChannel.Save(channel);
            changed++;
        }

        return changed;
    }

    #endregion
}
