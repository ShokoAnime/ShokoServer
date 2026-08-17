using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Base log query options: optional filters for JSONL log reads and downloads. Text fields use a
///   single prefix DSL string each; see remarks.
/// </summary>
/// <remarks>
///   <para><b>Grammar</b> (per text field value):</para>
///   <code>
///   &lt;field&gt; := &lt;no-colon-shorthand&gt; | &lt;mode&gt; &lt;modifiers&gt; ':' &lt;payload&gt;
///   &lt;modifiers&gt; := '' | '!' | '#' | '!' '#' | '#' '!'
///   </code>
///   <list type="bullet">
///     <item>If the string has <b>no ':'</b>, it is shorthand for <c>c:&lt;entire string&gt;</c> (substring, case-sensitive).</item>
///     <item><b>Mode</b> (first character): <c>c</c> contains, <c>=</c> equals, <c>^</c> starts with, <c>$</c> ends with, <c>~</c> fuzzy (always case-insensitive), <c>*</c> regex.</item>
///     <item>After the mode, before <c>:</c>: optional <c>!</c> (negate) and optional <c>#</c> (case-insensitive for <c>c</c>, <c>=</c>, <c>^</c>, <c>$</c>), each at most once, in either order.</item>
///     <item><c>#</c> is ignored for <c>~</c> and <c>*</c>.</item>
///     <item><b>Payload</b> is everything after the <b>first</b> <c>:</c> (e.g. <c>c:^:foo</c> contains the substring <c>^:foo</c>).</item>
///     <item><b>Regex</b> (<c>*:</c>): payload is a raw pattern or <c>/pattern/flags</c>; default case-sensitive; flag <c>i</c> = ignore case.</item>
///   </list>
///   <para><b>Prefix table</b></para>
///   <list type="table">
///     <listheader><term>Char</term><description>Meaning</description></listheader>
///     <item><term>c</term><description>Contains (substring)</description></item>
///     <item><term>=</term><description>Equals (full string)</description></item>
///     <item><term>^</term><description>Starts with</description></item>
///     <item><term>$</term><description>Ends with</description></item>
///     <item><term>~</term><description>Fuzzy match (see server implementation)</description></item>
///     <item><term>*</term><description>Regex</description></item>
///   </list>
///   <para><b>Examples:</b> <c>foo</c> → contains <c>foo</c>; <c>^#:GET</c>; <c>=!:x</c> (not equals <c>x</c>); <c>*:/error/i</c>; <c>=:</c> equals empty; <c>=!:</c> not equals empty (exception field: lines with exception text when using <c>entry.Exception ?? ""</c>).</para>
///   <para><b>Exception field:</b> when matching, <c>entry.Exception</c> is coalesced to <c>""</c> if null. <c>=:</c> matches no exception text; <c>=!:</c> matches lines with non-empty exception text.</para>
///   <para>A text property is <b>inactive</b> only when it is <c>null</c> (empty and whitespace-only strings are valid DSL).</para>
///   <para><b>HasFilters</b> considers only these filter fields (not dates, paging, or download format).</para>
/// </remarks>
public class LogBaseOptions
{
    /// <summary>
    ///   Inclusive start of timestamp range.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    ///   Inclusive end of timestamp range.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    ///   If non-empty, the entry level must be one of these values.
    /// </summary>
    public IReadOnlyList<LogLevel>? Levels { get; set; }

    /// <summary>
    ///   Optional DSL filter on <see cref="LogEntry.Logger"/>. Inactive when <c>null</c>.
    /// </summary>
    /// <remarks>
    ///   Same prefix DSL as <see cref="LogBaseOptions"/> remarks.
    /// </remarks>
    public string? Logger { get; set; }

    /// <summary>
    ///   Optional DSL filter on <see cref="LogEntry.Caller"/>. Inactive when <c>null</c>.
    /// </summary>
    public string? Caller { get; set; }

    /// <summary>
    ///   Optional DSL filter on <see cref="LogEntry.Message"/>. Inactive when <c>null</c>.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///   Optional DSL filter on exception text. Inactive when <c>null</c>.
    /// </summary>
    /// <remarks>
    ///   Same prefix DSL as <see cref="LogBaseOptions"/> remarks. The entry’s
    ///   exception is compared as <c>entry.Exception ?? ""</c>. Use <c>=:</c> for
    ///   lines with no exception; <c>=!:</c> for lines with exception text.
    /// </remarks>
    public string? Exception { get; set; }

    /// <summary>
    ///   If set, the entry process id must equal this value.
    /// </summary>
    public int? ProcessID { get; set; }

    /// <summary>
    ///   If set, the entry thread id must equal this value.
    /// </summary>
    public int? ThreadID { get; set; }

    /// <summary>
    ///   Indicates whether any filters are set.
    /// </summary>
    public bool HasFilters =>
        From.HasValue ||
        To.HasValue ||
        Levels is { Count: > 0 } ||
        Logger is not null ||
        Caller is not null ||
        Message is not null ||
        Exception is not null ||
        ProcessID.HasValue ||
        ThreadID.HasValue;
}
