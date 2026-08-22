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
