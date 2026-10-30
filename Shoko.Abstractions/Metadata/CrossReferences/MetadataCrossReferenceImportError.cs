namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   A line of a cross-reference file that could not be read.
/// </summary>
/// <param name="Line">The line's number, from 1.</param>
/// <param name="Message">What is wrong with it.</param>
public sealed record MetadataCrossReferenceImportError(int Line, string Message);
