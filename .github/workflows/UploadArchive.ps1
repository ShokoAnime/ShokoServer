$user = $env:FTP_USERNAME;
$password = $env:FTP_PASSWORD;
$ftp_server = $env:FTP_SERVER;
$current = [string](Get-Location);
$client = New-Object System.Net.WebClient;
$client.Credentials = New-Object System.Net.NetworkCredential($user, $password);
$client.UploadFile([string]($ftp_server) + "/shoko-server/daily/ShokoServer.zip", [string]($current) + "\\ShokoServer.zip");