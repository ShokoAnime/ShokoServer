$user = $env:FTP_USERNAME;
$password = $env:FTP_PASSWORD;
$server = $env:FTP_SERVER;
$current = [string](Get-Location);
$client = New-Object System.Net.WebClient;
$client.Credentials = New-Object System.Net.NetworkCredential($user, $password);
$client.UploadFile("$server/shoko-server/daily/ShokoServer.zip", "$current\\ShokoServer.zip");