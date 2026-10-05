using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.TextManagement;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
///   Works out which step of the chooser picked an entry's text, from the text
///   chosen and the language settings, for the text management routes.
/// </summary>
/// <remarks>
///   The chooser does not record its steps, so they are read back from the
///   text: a user's preference on it, the first configured language it
///   answers for a ranked source, or else the default. A Shoko series'
///   overview also says which of its fallbacks answered: the show behind the
///   first or a later linked season, or AniDB's notes.
/// </remarks>
internal static class TextChoiceExplainer
{
    #region Explain

    /// <summary>
    ///   Explains an entry's chosen text.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="chosen">The text chosen for the entry, or <c>null</c>.</param>
    /// <param name="defaultText">The entry's default, or <c>null</c>.</param>
    /// <param name="settings">The settings, which give the language and source orders.</param>
    /// <param name="seriesDescriptionStep">The step of a Shoko series' description walk, or <c>null</c> for any other choice.</param>
    /// <returns>The choice, with its step.</returns>
    internal static TextChoice Explain(
        MetadataGuid entityID,
        TextKind kind,
        IText? chosen,
        IText? defaultText,
        IServerSettings settings,
        SeriesDescriptionStep? seriesDescriptionStep = null
    )
    {
        var (step, language) = StepOf(entityID, kind, chosen, defaultText, settings, seriesDescriptionStep);
        return new()
        {
            Kind = kind,
            Step = step,
            Language = language?.GetString(),
            Source = chosen?.Source,
            Text = chosen is null ? null : new ManagedText(chosen),
            Default = defaultText is null ? null : new ManagedText(defaultText),
        };
    }

    /// <summary>
    ///   The step that picked a text, and the configured language it was
    ///   found in.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="chosen">The text chosen for the entry, or <c>null</c>.</param>
    /// <param name="defaultText">The entry's default, or <c>null</c>.</param>
    /// <param name="settings">The settings, which give the language and source orders.</param>
    /// <param name="seriesDescriptionStep">The step of a Shoko series' description walk, or <c>null</c> for any other choice.</param>
    /// <returns>The step, and the language for the steps that walk languages.</returns>
    internal static (TextChoiceStep Step, TitleLanguage? Language) StepOf(
        MetadataGuid entityID,
        TextKind kind,
        IText? chosen,
        IText? defaultText,
        IServerSettings settings,
        SeriesDescriptionStep? seriesDescriptionStep = null
    )
    {
        if (chosen is null)
            return (TextChoiceStep.None, null);

        if (chosen is ITitle { IsSynthesized: true })
            return (TextChoiceStep.Synthesized, null);

        if (chosen.Preference is TextPreference.Overall)
            return (TextChoiceStep.OverallPreference, null);

        var episode = entityID.EntityType == MetadataEntityType.Episode;
        var (languages, sourceOrder) = OrdersOf(kind, episode, settings);
        if (chosen.Preference is TextPreference.Language)
        {
            var spoken = languages.Where(language => language is not TitleLanguage.Main && TextChooser.Speaks(chosen, language)).ToList();
            return (TextChoiceStep.LanguagePreference, spoken.Count > 0 ? spoken[0] : chosen.Language);
        }

        var ranked = chosen.Source == MetadataSource.User || chosen.Source == entityID.Source || sourceOrder.Contains(chosen.Source);
        if (ranked)
        {
            foreach (var language in languages)
            {
                var answers = language is TitleLanguage.Main
                    ? chosen is ITitle { Type: TitleType.Main } || IText.Equals(chosen, defaultText)
                    : TextChooser.Speaks(chosen, language);
                if (!answers)
                    continue;

                var generic = episode && kind is TextKind.Title && GenericEpisodeTitles.LooksGeneric(chosen.Value);
                return (generic ? TextChoiceStep.GenericTitle : WalkStepOf(entityID, kind, chosen, seriesDescriptionStep), language);
            }
        }

        return (TextChoiceStep.Default, null);
    }

    /// <summary>
    ///   The step of the language walk that found a text: the walk itself, or
    ///   one of a Shoko series' overview fallbacks.
    /// </summary>
    /// <remarks>
    ///   AniDB's notes are only ever taken as the last resort, so the text
    ///   alone tells them apart.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="chosen">The text chosen for the entry.</param>
    /// <param name="seriesDescriptionStep">The step of a Shoko series' description walk, or <c>null</c> when not known.</param>
    /// <returns>The step.</returns>
    private static TextChoiceStep WalkStepOf(MetadataGuid entityID, TextKind kind, IText chosen, SeriesDescriptionStep? seriesDescriptionStep)
    {
        var seriesOverview = kind is TextKind.Overview && entityID.Source == MetadataSource.Shoko && entityID.EntityType == MetadataEntityType.Series;
        if (!seriesOverview)
            return TextChoiceStep.LanguageOrder;

        return seriesDescriptionStep switch
        {
            SeriesDescriptionStep.FirstSeasonShow => TextChoiceStep.FirstSeasonFallback,
            SeriesDescriptionStep.LaterSeasonShow => TextChoiceStep.ShowFallback,
            _ when chosen.Source == MetadataSource.AniDB && AnidbDescriptionMarkup.IsNoteOnly(chosen.Value) => TextChoiceStep.NoteFallback,
            _ => TextChoiceStep.LanguageOrder,
        };
    }

    /// <summary>
    ///   The languages and sources the chooser walks for a kind of text.
    /// </summary>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="episode">Whether the entry is an episode, which has its own title orders.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The languages, best first, and the sources, best first.</returns>
    private static (IReadOnlyList<TitleLanguage> Languages, IReadOnlyList<MetadataSource> SourceOrder) OrdersOf(TextKind kind, bool episode, IServerSettings settings)
    {
        var language = settings.Language;
        var (codes, sources) = kind switch
        {
            TextKind.Overview => (language.DescriptionLanguageOrder, language.DescriptionSourceOrder),
            _ when episode => (language.EpisodeTitleLanguageOrder, language.EpisodeTitleSourceOrder),
            _ => (language.SeriesTitleLanguageOrder, language.SeriesTitleSourceOrder),
        };

        // Read as the chooser reads them (see Languages).
        var languages = codes
            .Where(code => !string.IsNullOrEmpty(code))
            .Select(code => code.GetTitleLanguage())
            .Where(value => value is not (TitleLanguage.Unknown or TitleLanguage.None))
            .Distinct()
            .ToList();
        return (languages, sources);
    }

    #endregion
}
