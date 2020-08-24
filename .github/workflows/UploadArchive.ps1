user = $env:FTP_USERNAME;
password = $env:FTP_PASSWORD;
server = $env:FTP_SERVER;
client = New-Object System.Net.WebClient;
client.Credentials = New-Object System.Net.NetworkCredential("$user", "$password");
client.UploadFile("$(server)/files/shoko-server/daily/ShokoServer.zip", ".\\ShokoServer.zip");