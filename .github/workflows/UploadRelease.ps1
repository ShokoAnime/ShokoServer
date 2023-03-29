Param(
    [string] $remote = "Artifact.bin",
    [string] $local = "Artifact.bin"
)

$username = $env:FTP_USERNAME;
$password = $env:FTP_PASSWORD;
$ftp_server = $env:FTP_SERVER;

$current = [string](Get-Location);
$client = New-Object System.Net.WebClient;
$client.Credentials = New-Object System.Net.NetworkCredential($username, $password);
$client.UploadFile("$ftp_server/files/shoko-server/$remote", "$current\\$local");
