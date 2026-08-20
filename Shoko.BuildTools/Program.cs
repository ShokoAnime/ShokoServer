using Shoko.BuildTools;

// ── Argument parsing ───────────────────────────────────────────────────
// Our args: --manifest|-m <path>, --prune|-p <count>, --prune-method <channel|global>,
//           --url <download-url>, --output <zip-path>, --channel <stable|dev|debug>,
//           --tag|-t <tag>, --release-notes <text>, --release-notes-path <path>
// Everything else is forwarded to dotnet build.

var argsList = args.ToList();
var manifestPath = (string?)null;
var pruneCount = (int?)null;
var pruneMethod = "channel";
var downloadUrl = (string?)null;
var outputZip = (string?)null;
var channel = (string?)null;
var releaseTag = (string?)null;
var releaseNotes = (string?)null;
var releaseNotesPath = (string?)null;
var forwardArgs = new List<string>();

for (var i = 0; i < argsList.Count; i++)
{
    var arg = argsList[i];

    if (arg is "--")
    {
        if (i + 1 < argsList.Count)
            forwardArgs.AddRange(argsList.GetRange(i + 1, argsList.Count - 1 - i));
        break;
    }

    if (arg is "--manifest" or "-m" && i + 1 < argsList.Count)
    {
        manifestPath = argsList[++i];
    }
    else if (arg is "--prune" or "-p" && i + 1 < argsList.Count)
    {
        if (int.TryParse(argsList[++i], out var count) && count > 0)
            pruneCount = count;
    }
    else if (arg is "--prune-method" && i + 1 < argsList.Count)
    {
        var method = argsList[++i].ToLowerInvariant();
        pruneMethod = method is "channel" or "global" ? method : "channel";
    }
    else if (arg is "--url" or "-u" && i + 1 < argsList.Count)
    {
        downloadUrl = argsList[++i];
    }
    else if (arg is "--output" or "-o" && i + 1 < argsList.Count)
    {
        outputZip = argsList[++i];
    }
    else if (arg is "--channel" && i + 1 < argsList.Count)
    {
        channel = argsList[++i];
    }
    else if (arg is "--tag" or "-t" && i + 1 < argsList.Count)
    {
        releaseTag = argsList[++i];
    }
    else if (arg is "--release-notes" && i + 1 < argsList.Count)
    {
        releaseNotes = argsList[++i];
    }
    else if (arg is "--release-notes-path" && i + 1 < argsList.Count)
    {
        releaseNotesPath = argsList[++i];
    }
    else
    {
        forwardArgs.Add(arg);
    }
}

// ── Release notes ──────────────────────────────────────────────────────
// --release-notes, then --release-notes-path, then RELEASE_NOTES. Both
// flags at once is a contradiction, and an unreadable path is fatal:
// recording no notes silently is what this flag exists to prevent.
if (releaseNotes is not null && releaseNotesPath is not null)
{
    Console.Error.WriteLine("--release-notes and --release-notes-path are mutually exclusive.");
    return 1;
}

if (releaseNotesPath is not null)
{
    try
    {
        releaseNotes = await File.ReadAllTextAsync(releaseNotesPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not read --release-notes-path '{releaseNotesPath}': {ex.Message}");
        return 1;
    }
}

releaseNotes ??= Environment.GetEnvironmentVariable("RELEASE_NOTES");

// Empty is absent; a manifest says "no notes" with null.
if (string.IsNullOrWhiteSpace(releaseNotes))
    releaseNotes = null;
else
    releaseNotes = releaseNotes.Trim();

var missingArgs = new List<string>();
if (string.IsNullOrWhiteSpace(downloadUrl))
    missingArgs.Add("--url");
if (string.IsNullOrWhiteSpace(outputZip))
    missingArgs.Add("--output");

if (missingArgs.Count > 0)
{
    Console.Error.WriteLine($"Missing required argument(s): {string.Join(", ", missingArgs)}.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  --output <path>  where to write the packed archive");
    Console.Error.WriteLine("  --url <url>      the address that archive will be downloaded from");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Both accept the {runtime}, {version}, {name}, {abstraction} and {tag}");
    Console.Error.WriteLine("templates, so one invocation covers every runtime identifier. For example:");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  --output dist/{name}-{tag}-{runtime}.zip \\");
    Console.Error.WriteLine("  --url https://example.org/owner/repo/releases/download/{tag}/{name}-{tag}-{runtime}.zip");
    return 1;
}

var exitCode = await BuildCommand.RunAsync(
    manifestPath,
    pruneCount,
    pruneMethod,
    downloadUrl!,
    outputZip!,
    channel,
    releaseTag,
    releaseNotes,
    [.. forwardArgs]
);

return exitCode;
