Param(
    [string] $dsn = "SENTRY_DSN_KEY_GOES_HERE"
)
$folderPath = "./Shoko.Server"
$searchString = "SENTRY_DSN_KEY_GOES_HERE"

Get-ChildItem -Path $folderPath -Include "*.cs" -Recurse | ForEach-Object {
    (Get-Content $_.FullName) | ForEach-Object {
        $_ -replace $searchString, $dsn
    } | Set-Content $_.FullName
}
