Param(
    [string] $dsn = "SENTRY_DSN_KEY_GOES_HERE"
)

$filename = "./Shoko.Server/Server/Constants.cs"
$searchString = "SENTRY_DSN_KEY_GOES_HERE"

(Get-Content $filename) | ForEach-Object {
    $_ -replace $searchString, $dsn
} | Set-Content $filename
