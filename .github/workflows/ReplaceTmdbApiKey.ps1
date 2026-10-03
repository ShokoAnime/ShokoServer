Param(
    [string] $apiKey = "TMDB_API_KEY_GOES_HERE"
)

$filename = "./Shoko.Plugin.Tmdb/Constants.cs"
$searchString = "TMDB_API_KEY_GOES_HERE"

(Get-Content $filename) | ForEach-Object {
    $_ -replace $searchString, $apiKey
} | Set-Content $filename
