using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Text.Options;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.TextManagement;
using Shoko.Server.API.v3.Models.TextManagement.Input;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
///   Manages the stored titles and overviews of every entry, through
///   <see cref="IMetadataTextManager"/>: listing and looking them up, adding,
///   changing, enabling and removing a user's texts, and picking the text an
///   entry is called or described by.
/// </summary>
/// <remarks>
///   Titles and overviews are kept apart, and their IDs are unique only among
///   texts of the same kind, so a text is named by its kind and its ID.
/// </remarks>
/// <param name="textManager">The text manager.</param>
/// <param name="metadataService">Finds the entries the routes name.</param>
/// <param name="settingsProvider">The settings provider.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/Text/Management"), Tags("Text")]
[ApiV3]
[Authorize]
public class TextManagementController(IMetadataTextManager textManager, IMetadataService metadataService, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    private const string TextNotFound = "The requested text does not exist.";
    private const string EntityNotFound = "The requested entity does not exist.";
    private const string KindRequired = "Name whether a title or an overview is meant.";

    #region Texts | Query

    /// <summary>
    ///   Get every stored text, with optional filtering and pagination.
    /// </summary>
    /// <param name="kind">Only titles or only overviews; both, titles first, when left out.</param>
    /// <param name="entitySource">Only texts of entries from this source.</param>
    /// <param name="entityType">Only texts of entries of this kind.</param>
    /// <param name="source">Only texts from this source.</param>
    /// <param name="language">Only texts in these languages, comma separated.</param>
    /// <param name="titleType">Only titles of this type. Leaves out every overview.</param>
    /// <param name="preference">Only texts with this preference.</param>
    /// <param name="isEnabled">Only enabled or only disabled texts; both when left out.</param>
    /// <param name="pageSize">Number of results per page (0-100, default 50).</param>
    /// <param name="page">Page number (default 1).</param>
    /// <returns>A paginated list of texts.</returns>
    [HttpGet]
    public ActionResult<ListResult<ManagedText>> GetAllTexts(
        [FromQuery] TextKind? kind = null,
        [FromQuery] MetadataSource? entitySource = null,
        [FromQuery] MetadataEntityType? entityType = null,
        [FromQuery] MetadataSource? source = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] TitleType? titleType = null,
        [FromQuery] TextPreference? preference = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var options = Options(source, language, titleType, preference, isEnabled, false);
        options.EntitySource = entitySource;
        options.EntityType = entityType;
        var texts = KindsOf(kind).SelectMany(textKind => textManager.GetAllTexts(textKind, options));
        return Page(Filter(texts, language), page, pageSize);
    }

    /// <summary>
    ///   Get a stored text by its kind and ID.
    /// </summary>
    /// <param name="kind">Whether the ID names a title or an overview.</param>
    /// <param name="textID">The text's ID.</param>
    /// <returns>The text if found, otherwise 404.</returns>
    [HttpGet("{kind}/{textID:int}")]
    public ActionResult<ManagedText> GetTextByID([FromRoute] TextKind kind, [FromRoute, Range(1, int.MaxValue)] int textID)
    {
        if (Find(kind, textID) is not { } text)
            return NotFound(TextNotFound);
        return new ManagedText(text);
    }

    /// <summary>
    ///   Get the stored texts of entries that can no longer be found, with
    ///   optional filtering and pagination.
    /// </summary>
    /// <remarks>
    ///   An entry only a plugin can find counts as gone while that plugin is
    ///   not loaded.
    /// </remarks>
    /// <param name="entitySource">Only texts of entries from this source.</param>
    /// <param name="kind">Only titles or only overviews; both when left out.</param>
    /// <param name="pageSize">Number of results per page (0-100, default 50).</param>
    /// <param name="page">Page number (default 1).</param>
    /// <returns>A paginated list of orphaned texts.</returns>
    [HttpGet("Orphaned")]
    public ActionResult<ListResult<ManagedText>> GetOrphanedTexts(
        [FromQuery] MetadataSource? entitySource = null,
        [FromQuery] TextKind? kind = null,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var options = new TextFilteringOptions { IsEnabled = null, IncludeInlineDefault = false };
        var texts = textManager.GetOrphanedEntries(entitySource)
            .SelectMany(entityID => KindsOf(kind).SelectMany(textKind => TextsOf(entityID, textKind, options)));
        return Page(texts, page, pageSize);
    }

    #endregion

    #region Texts | Mutations

    /// <summary>
    ///   Change a stored text in part. Only a text a user added can be given
    ///   a new value.
    /// </summary>
    /// <param name="kind">Whether the ID names a title or an overview.</param>
    /// <param name="textID">The text's ID.</param>
    /// <param name="body">What to change.</param>
    /// <returns>The text as it is now.</returns>
    [Authorize("admin")]
    [HttpPatch("{kind}/{textID:int}")]
    public ActionResult<ManagedText> UpdateText(
        [FromRoute] TextKind kind,
        [FromRoute, Range(1, int.MaxValue)] int textID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateTextBody body
    )
    {
        if (Find(kind, textID) is not { } text)
            return NotFound(TextNotFound);
        try
        {
            return new ManagedText(textManager.UpdateText(text, body.ToTextUpdateData()));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.Value));
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.Value));
        }
    }

    /// <summary>
    ///   Change several stored texts in part, each in its own way, such as
    ///   disabling or re-ordering an entry's titles in one call.
    /// </summary>
    /// <param name="body">The texts and what to change on each.</param>
    /// <returns>The texts changed, as they are now, or 400 when none could be.</returns>
    [Authorize("admin")]
    [HttpPost("Batch/Update")]
    public ActionResult<ListResult<ManagedText>> BatchUpdateTexts(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] BatchUpdateTextBody body
    )
    {
        var results = new List<ManagedText>();
        var errors = new List<string>();
        foreach (var item in body.Texts)
        {
            if (item.Kind is not { } kind || item.ID is not { } textID)
            {
                errors.Add(KindRequired);
                continue;
            }

            if (Find(kind, textID) is not { } text)
            {
                errors.Add($"{kind} {textID} not found.");
                continue;
            }

            try
            {
                results.Add(new ManagedText(textManager.UpdateText(text, item.ToTextUpdateData())));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add($"{kind} {textID}: {ex.Message}");
            }
        }

        if (errors.Count > 0 && results.Count == 0)
            return ValidationProblem(string.Join(" ", errors), nameof(body.Texts));
        return results.ToListResult();
    }

    /// <summary>
    ///   Enable or disable a stored text. A disabled text is kept, so a
    ///   refresh does not bring it back, but it is neither listed nor chosen.
    /// </summary>
    /// <param name="kind">Whether the ID names a title or an overview.</param>
    /// <param name="textID">The text's ID.</param>
    /// <param name="body">The enabled state to set.</param>
    /// <returns>The text as it is now.</returns>
    [Authorize("admin")]
    [HttpPost("{kind}/{textID:int}/Enabled")]
    public ActionResult<ManagedText> EnableOrDisableText(
        [FromRoute] TextKind kind,
        [FromRoute, Range(1, int.MaxValue)] int textID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] EnableTextBody body
    )
    {
        if (Find(kind, textID) is not { } text)
            return NotFound(TextNotFound);
        return new ManagedText(textManager.EnableText(text, body.Enabled));
    }

    /// <summary>
    ///   Make a stored text the one its entry is called or described by,
    ///   overall or for the text's own language.
    /// </summary>
    /// <param name="kind">Whether the ID names a title or an overview.</param>
    /// <param name="textID">The text's ID.</param>
    /// <param name="languageOnly">Prefer it only once the language order reaches its language, rather than over everything.</param>
    /// <returns>The text as it is now.</returns>
    [Authorize("admin")]
    [HttpPut("{kind}/{textID:int}/Preferred")]
    public ActionResult<ManagedText> SetPreferredText(
        [FromRoute] TextKind kind,
        [FromRoute, Range(1, int.MaxValue)] int textID,
        [FromQuery] bool languageOnly = false
    )
    {
        if (Find(kind, textID) is not { EntityID: { } entityID } text)
            return NotFound(TextNotFound);
        return Prefer(entityID, kind, text, languageOnly);
    }

    /// <summary>
    ///   Take a user's preference off a stored text. A pick of another text
    ///   has no other use, so it is removed.
    /// </summary>
    /// <param name="kind">Whether the ID names a title or an overview.</param>
    /// <param name="textID">The text's ID.</param>
    /// <returns>No content, or 400 when the text carried no preference.</returns>
    [Authorize("admin")]
    [HttpDelete("{kind}/{textID:int}/Preferred")]
    public ActionResult UnsetPreferredText([FromRoute] TextKind kind, [FromRoute, Range(1, int.MaxValue)] int textID)
    {
        if (Find(kind, textID) is not { } text)
            return NotFound(TextNotFound);
        if (!textManager.UnsetPreferredText(text))
            return ValidationProblem("The text carries no preference.", nameof(textID));
        return NoContent();
    }

    /// <summary>
    ///   Remove a stored text, and every pick of it. A source's own text comes
    ///   back on its next refresh; disable it to keep it away.
    /// </summary>
    /// <param name="kind">Whether the ID names a title or an overview.</param>
    /// <param name="textID">The text's ID.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("{kind}/{textID:int}")]
    public ActionResult RemoveText([FromRoute] TextKind kind, [FromRoute, Range(1, int.MaxValue)] int textID)
    {
        if (Find(kind, textID) is not { } text)
            return NotFound(TextNotFound);
        textManager.RemoveText(text);
        return NoContent();
    }

    /// <summary>
    ///   Remove the stored texts of every entry that can no longer be found.
    /// </summary>
    /// <param name="entitySource">Only entries from this source.</param>
    /// <returns>The number of texts removed.</returns>
    [Authorize("admin")]
    [HttpDelete("Orphaned")]
    public ActionResult<int> PurgeOrphanedTexts([FromQuery] MetadataSource? entitySource = null)
        => Ok(textManager.PurgeOrphanedTexts(entitySource));

    #endregion

    #region Entities

    /// <summary>
    ///   Get every text an entry is chosen from: the default kept on its own
    ///   row, as a read-only entry with no ID, and the stored texts, with
    ///   those of the entries it is linked to for a Shoko entry.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="kind">Only titles or only overviews; both, titles first, when left out.</param>
    /// <param name="source">Only texts from this source.</param>
    /// <param name="language">Only texts in these languages, comma separated.</param>
    /// <param name="titleType">Only titles of this type. Leaves out every overview.</param>
    /// <param name="isEnabled">Only enabled or only disabled texts; both when left out.</param>
    /// <param name="pageSize">Number of results per page (0-100, default 50).</param>
    /// <param name="page">Page number (default 1).</param>
    /// <returns>A paginated list of the entry's texts, each marked as its default and chosen text or not.</returns>
    [HttpGet("Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<ListResult<ManagedText>> GetTextsForEntity(
        [FromRoute] MetadataSource entitySource,
        [FromRoute] MetadataEntityType entityType,
        [FromRoute] string entityID,
        [FromQuery] TextKind? kind = null,
        [FromQuery] MetadataSource? source = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] TitleType? titleType = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (FindEntity(entitySource, entityType, entityID) is not { } id)
            return NotFound(EntityNotFound);

        var options = Options(source, language, titleType, null, isEnabled, true);
        var texts = new List<ManagedText>();
        foreach (var textKind in KindsOf(kind))
        {
            var defaultText = DefaultOf(id, textKind);
            var preferred = PreferredOf(id, textKind);
            var listed = Filter(TextsOf(id, textKind, options), language);
            texts.AddRange(listed.Select(text => new ManagedText(text, Same(text, defaultText), Same(text, preferred))));
        }

        return texts.ToListResult(page, pageSize);
    }

    /// <summary>
    ///   Add a user's text to an entry.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="body">The text to add.</param>
    /// <returns>The stored text.</returns>
    [Authorize("admin")]
    [HttpPost("Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<ManagedText> AddText(
        [FromRoute] MetadataSource entitySource,
        [FromRoute] MetadataEntityType entityType,
        [FromRoute] string entityID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AddTextBody body
    )
    {
        if (body.Kind is not { } kind)
            return ValidationProblem(KindRequired, nameof(body.Kind));
        if (FindEntity(entitySource, entityType, entityID) is not { } id)
            return NotFound(EntityNotFound);
        try
        {
            var text = textManager.AddText(id, body.ToTextData());
            return Created($"/api/v3/Text/Management/{kind}/{text.ID}", new ManagedText(text));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    /// <summary>
    ///   Remove an entry's stored texts. A source's own texts come back on its
    ///   next refresh.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="source">Only this source's texts; every source's when left out.</param>
    /// <returns>The number of texts removed.</returns>
    [Authorize("admin")]
    [HttpDelete("Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<int> RemoveTextsForEntity(
        [FromRoute] MetadataSource entitySource,
        [FromRoute] MetadataEntityType entityType,
        [FromRoute] string entityID,
        [FromQuery] MetadataSource? source = null
    )
    {
        if (MetadataEntryController.ToGuid(entitySource, entityType, entityID) is not { } id)
            return NotFound(EntityNotFound);
        return Ok(textManager.RemoveTexts(id, source));
    }

    /// <summary>
    ///   Get the text chosen for an entry, and the step of the chooser that
    ///   picked it.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="kind">The title or the overview. Defaults to the title.</param>
    /// <returns>The choice.</returns>
    [HttpGet("Preferred/Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<TextChoice> GetPreferredTextForEntity(
        [FromRoute] MetadataSource entitySource,
        [FromRoute] MetadataEntityType entityType,
        [FromRoute] string entityID,
        [FromQuery] TextKind kind = TextKind.Title
    )
    {
        if (FindEntity(entitySource, entityType, entityID) is not { } id)
            return NotFound(EntityNotFound);
        return TextChoiceExplainer.Explain(id, kind, PreferredOf(id, kind), DefaultOf(id, kind), SettingsProvider.GetSettings());
    }

    /// <summary>
    ///   Pick the text an entry is called or described by, overall or for the
    ///   text's own language: a stored text of this or any other entry, the
    ///   entry's default, or a new value. Anything but a stored text of the
    ///   entry itself is stored on it as a <c>user</c> pick.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="body">What to pick.</param>
    /// <returns>The stored text that now carries the preference.</returns>
    [Authorize("admin")]
    [HttpPut("Preferred/Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<ManagedText> SetPreferredTextForEntity(
        [FromRoute] MetadataSource entitySource,
        [FromRoute] MetadataEntityType entityType,
        [FromRoute] string entityID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SetPreferredTextBody body
    )
    {
        if (body.Kind is not { } kind)
            return ValidationProblem(KindRequired, nameof(body.Kind));
        if (FindEntity(entitySource, entityType, entityID) is not { } id)
            return NotFound(EntityNotFound);

        var named = (body.TextID is not null ? 1 : 0) + (body.Default ? 1 : 0) + (body.Value is not null ? 1 : 0);
        if (named != 1)
            return ValidationProblem("Name exactly one of a stored text, the default or a new value.", nameof(body.TextID));

        IText text;
        if (body.TextID is { } textID)
        {
            if (Find(kind, textID) is not { } found)
                return NotFound(TextNotFound);
            text = found;
        }
        else if (body.Default)
        {
            if (DefaultOf(id, kind) is not { } defaultText)
                return ValidationProblem("The entry has no default to pick.", nameof(body.Default));
            text = defaultText;
        }
        else
        {
            var languageCode = string.IsNullOrWhiteSpace(body.LanguageCode) ? "unk" : body.LanguageCode.Trim();
            var countryCode = string.IsNullOrWhiteSpace(body.CountryCode) ? null : body.CountryCode.Trim();
            var language = countryCode is null ? languageCode.GetTitleLanguage() : languageCode.GetTitleLanguage(countryCode);
            text = kind is TextKind.Title
                ? new TitleStub { Source = MetadataSource.User, Value = body.Value!, Language = language, LanguageCode = languageCode, CountryCode = countryCode, Type = TitleType.None }
                : new TextStub { Source = MetadataSource.User, Value = body.Value!, Language = language, LanguageCode = languageCode, CountryCode = countryCode };
        }

        return Prefer(id, kind, text, body.LanguageOnly);
    }

    /// <summary>
    ///   Take every preference off an entry's stored texts, removing the picks
    ///   of other texts.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="kind">Only titles or only overviews; both when left out.</param>
    /// <returns>The number of texts that carried a preference.</returns>
    [Authorize("admin")]
    [HttpDelete("Preferred/Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<int> UnsetAllPreferredTextsForEntity(
        [FromRoute] MetadataSource entitySource,
        [FromRoute] MetadataEntityType entityType,
        [FromRoute] string entityID,
        [FromQuery] TextKind? kind = null
    )
    {
        if (MetadataEntryController.ToGuid(entitySource, entityType, entityID) is not { } id)
            return NotFound(EntityNotFound);
        return Ok(textManager.UnsetAllPreferredTexts(id, kind));
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   The kinds a route reads.
    /// </summary>
    /// <param name="kind">One kind, or <c>null</c> for both.</param>
    /// <returns>The kinds, titles first.</returns>
    private static TextKind[] KindsOf(TextKind? kind)
        => kind is { } one ? [one] : [TextKind.Title, TextKind.Overview];

    /// <summary>
    ///   A stored text by its kind and ID.
    /// </summary>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="textID">The text's ID.</param>
    /// <returns>The text, or <c>null</c> when none has that ID.</returns>
    private IText? Find(TextKind kind, int textID)
        => kind is TextKind.Title ? textManager.GetTitleByID(textID) : textManager.GetOverviewByID(textID);

    /// <summary>
    ///   The entry a route names, when it can be found.
    /// </summary>
    /// <param name="entitySource">The source of the entry.</param>
    /// <param name="entityType">The kind of the entry.</param>
    /// <param name="entityID">The source's own ID for the entry.</param>
    /// <returns>The entry's ID, or <c>null</c> when the ID is not valid or names nothing.</returns>
    private MetadataGuid? FindEntity(MetadataSource entitySource, MetadataEntityType entityType, string entityID)
        => MetadataEntryController.ToGuid(entitySource, entityType, entityID) is { } id && metadataService.GetEntry(id) is not null ? id : null;

    /// <summary>
    ///   An entry's texts of one kind.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="options">Which texts to list.</param>
    /// <returns>The texts.</returns>
    private IReadOnlyList<IText> TextsOf(MetadataGuid entityID, TextKind kind, TextFilteringOptions options)
        => kind is TextKind.Title ? textManager.GetTitles(entityID, options) : textManager.GetOverviews(entityID, options);

    /// <summary>
    ///   An entry's default text of one kind.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <returns>The default, or <c>null</c> when there is none.</returns>
    private IText? DefaultOf(MetadataGuid entityID, TextKind kind)
        => kind is TextKind.Title ? textManager.GetDefaultTitle(entityID) : textManager.GetDefaultOverview(entityID);

    /// <summary>
    ///   An entry's chosen text of one kind.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <returns>The chosen text, or <c>null</c> when there is none.</returns>
    private IText? PreferredOf(MetadataGuid entityID, TextKind kind)
        => kind is TextKind.Title ? textManager.GetPreferredTitle(entityID) : textManager.GetPreferredOverview(entityID);

    /// <summary>
    ///   Puts a preference on a text for an entry.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="text">The text to prefer.</param>
    /// <param name="languageOnly">Whether to prefer it for its language only.</param>
    /// <returns>The stored text carrying the preference, or a validation problem.</returns>
    private ActionResult<ManagedText> Prefer(MetadataGuid entityID, TextKind kind, IText text, bool languageOnly)
    {
        try
        {
            var preferred = kind is TextKind.Title && text is ITitle title
                ? textManager.SetPreferredTitle(entityID, title, languageOnly)
                : textManager.SetPreferredOverview(entityID, text, languageOnly);
            return new ManagedText(preferred);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    /// <summary>
    ///   The filtering options a route's query gives.
    /// </summary>
    /// <param name="source">Only texts from this source.</param>
    /// <param name="language">The languages asked for; one is passed on, more are filtered after.</param>
    /// <param name="titleType">Only titles of this type.</param>
    /// <param name="preference">Only texts with this preference.</param>
    /// <param name="isEnabled">Only enabled or disabled texts, or both.</param>
    /// <param name="includeInlineDefault">Whether to list the default kept on the entry's row.</param>
    /// <returns>The options.</returns>
    private static TextFilteringOptions Options(MetadataSource? source, HashSet<TitleLanguage>? language, TitleType? titleType, TextPreference? preference, bool? isEnabled, bool includeInlineDefault)
        => new()
        {
            Source = source,
            Language = language is { Count: 1 } ? language.First() : null,
            TitleType = titleType,
            Preference = preference,
            IsEnabled = isEnabled,
            IncludeInlineDefault = includeInlineDefault,
        };

    /// <summary>
    ///   Keeps the texts in the languages asked for, when more than one was.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <param name="language">The languages, or <c>null</c> for all.</param>
    /// <returns>The texts kept.</returns>
    private static IEnumerable<IText> Filter(IEnumerable<IText> texts, HashSet<TitleLanguage>? language)
        => language is { Count: > 1 } ? texts.Where(text => language.Contains(text.Language)) : texts;

    /// <summary>
    ///   Whether two texts are the same: the same stored text, or equal
    ///   values for texts that are not stored.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="other">The other text, or <c>null</c>.</param>
    /// <returns><c>true</c> when they are the same.</returns>
    private static bool Same(IText text, IText? other)
        => other is not null && (text.ID is { } id && other.ID is { } otherID
            ? id == otherID && text.EntityID == other.EntityID
            : IText.Equals(text, other));

    /// <summary>
    ///   One page of texts, counting them all without holding them.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <param name="page">The page, from <c>1</c>.</param>
    /// <param name="pageSize">The page size, or <c>0</c> for every text.</param>
    /// <returns>The page.</returns>
    private static ListResult<ManagedText> Page(IEnumerable<IText> texts, int page, int pageSize)
    {
        var skip = pageSize <= 0 ? 0 : pageSize * (page - 1);
        var total = 0;
        var list = new List<ManagedText>();
        foreach (var text in texts)
        {
            if (total >= skip && (pageSize <= 0 || list.Count < pageSize))
                list.Add(new ManagedText(text));
            total++;
        }

        return new() { Total = total, List = list };
    }

    #endregion
}
