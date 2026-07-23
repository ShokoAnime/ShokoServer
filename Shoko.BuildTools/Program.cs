using Shoko.BuildTools;

// ── Argument parsing ───────────────────────────────────────────────────
// Our args: --manifest|-m <path>, --prune|-p <count>, --prune-method <channel|global>,
//           --url <download-url>, --output <zip-path>
// Everything else is forwarded to dotnet build.

var argsList = args.ToList();
var manifestPath = (string?)null;
var pruneCount = (int?)null;
var pruneMethod = "channel";
var downloadUrl = (string?)null;
var outputZip = (string?)null;
var forwardArgs = new List<string>();

for (var i = 0; i < argsList.Count; i++)
{
    var arg = argsList[i];

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
    else
    {
        forwardArgs.Add(arg);
    }
}

var exitCode = await BuildCommand.RunAsync(
    manifestPath,
    pruneCount,
    pruneMethod,
    downloadUrl,
    outputZip,
    [.. forwardArgs]
);

return exitCode;
