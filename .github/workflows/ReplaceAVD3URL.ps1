Param(
    [string] $url = "AVD3_URL_GOES_HERE"
)

$filename = "./Shoko.Server/Utilities/AVDumpHelper.cs"
$searchString = "AVD3_URL_GOES_HERE"

(Get-Content $filename) | ForEach-Object {
    $_ -replace $searchString, $url
} | Set-Content $filename
