; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SHOKO0001 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a collection nested directly inside another collection cannot be described by the UI schema.
SHOKO0002 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a dictionary key that is not serializable to text makes UI schema generation throw.
SHOKO0003 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a `[List]` display type that the element type cannot support makes UI schema generation throw.
SHOKO0004 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a complex `[List]` display type without a `[Key]` property makes UI schema generation throw.
SHOKO0005 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a record-shaped property that is not a generic dictionary makes UI schema generation throw.
SHOKO0006 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a condition that could never hold makes UI schema generation throw.
SHOKO0007 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a handler that cannot react to what it names makes UI schema generation throw.
SHOKO0008 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, an options provider method that is not public, is generic or returns no collection makes UI schema generation throw.
SHOKO0009 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, an options provider naming no member, a missing one or one twice makes UI schema generation throw.
SHOKO0010 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, an options provider naming a member part that cannot take options makes UI schema generation throw.
SHOKO0011 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, options of a type that cannot be told apart as text make UI schema generation throw.
SHOKO0012 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, options that do not match their members make UI schema generation throw.
SHOKO0013 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a member part with two options providers makes UI schema generation throw.
SHOKO0014 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a flags enum without single-bit members makes UI schema generation throw.
SHOKO0015 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, an options key parameter on a provider that has no key to hand makes UI schema generation throw.
SHOKO0016 | Shoko.Configuration | Error | ConfigurationTypeAnalyzer, a checkbox section without a bool switch, or another section naming one, makes UI schema generation throw.
