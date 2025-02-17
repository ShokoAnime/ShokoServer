using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Data.TypeConverters;

public class TitleLanguageToString() : ValueConverter<TitleLanguage, string>(t => t.GetString(), s => s.GetTitleLanguage());
public class TitleLanguageToInt() : ValueConverter<TitleLanguage, int>(t => (int)t, i => (TitleLanguage)i);
public class TitleLanguageToLong() : ValueConverter<TitleLanguage, long>(t => (long)t, i => (TitleLanguage)i);
