using System;
using System.Collections.Generic;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;

namespace Shoko.Server.Services.MetadataStorage;

/// <summary>
///   Checks and plans the content ratings the series and movie stores keep
///   with the entries they rate.
/// </summary>
internal static class MetadataContentRatings
{
    #region Validation

    /// <summary>
    ///   The longest rating the table holds.
    /// </summary>
    internal const int MaxRatingLength = 128;

    /// <summary>
    ///   Checks the ratings given for an entry before anything is written,
    ///   keeping the first rating given for each country.
    /// </summary>
    /// <param name="ratings">The ratings, which may be left out.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>The ratings to store, in order, with their language filled in.</returns>
    /// <exception cref="ArgumentNullException">A rating, its country or its value is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A country or language code, or a rating, is blank or too long.</exception>
    internal static List<MetadataContentRatingData> Check(IReadOnlyList<MetadataContentRatingData>? ratings, string paramName)
    {
        var checkedRatings = new List<MetadataContentRatingData>();
        var countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rating in ratings ?? [])
        {
            ArgumentNullException.ThrowIfNull(rating, paramName);
            ArgumentNullException.ThrowIfNull(rating.CountryCode, paramName);
            ArgumentNullException.ThrowIfNull(rating.Rating, paramName);
            var countryCode = MetadataEntries.CheckLanguageCode(rating.CountryCode, paramName)
                ?? throw new ArgumentException("A content rating must name its country.", paramName);
            if (string.IsNullOrWhiteSpace(rating.Rating) || rating.Rating.Length > MaxRatingLength)
                throw new ArgumentException($"A content rating must be 1 to {MaxRatingLength} characters, but got '{rating.Rating}'.", paramName);
            if (!countries.Add(countryCode))
                continue;

            var languageCode = MetadataEntries.CheckLanguageCode(rating.LanguageCode, paramName) ?? countryCode.FromIso3166ToIso639();
            checkedRatings.Add(rating with { CountryCode = countryCode, LanguageCode = languageCode ?? string.Empty });
        }

        return checkedRatings;
    }

    #endregion

    #region Planning

    /// <summary>
    ///   Works out which rows make an entry's ratings the ones given. A
    ///   country's row is reused, and left out when nothing about it changed.
    /// </summary>
    /// <param name="repository">The ratings' table.</param>
    /// <param name="entry">The rated entry.</param>
    /// <param name="ratings">The checked ratings, in order.</param>
    /// <returns>The rows to save and the rows to remove.</returns>
    internal static (List<Metadata_ContentRating> Saving, List<Metadata_ContentRating> Deleting) Plan(
        Metadata_ContentRatingRepository repository,
        MetadataGuid entry,
        IReadOnlyList<MetadataContentRatingData> ratings
    )
        => MetadataRows.Replace(
            repository.GetByEntry(entry),
            ratings,
            row => row.CountryCode.ToUpperInvariant(),
            rating => rating.CountryCode.ToUpperInvariant(),
            (row, rating, position) =>
            {
                MetadataRows.Place(row, entry, position);
                row.CountryCode = rating.CountryCode;
                row.LanguageCode = rating.LanguageCode ?? string.Empty;
                row.Rating = rating.Rating;
            },
            (stored, row) =>
                stored.Ordering == row.Ordering &&
                stored.CountryCode == row.CountryCode &&
                stored.LanguageCode == row.LanguageCode &&
                stored.Rating == row.Rating
        );

    #endregion
}
