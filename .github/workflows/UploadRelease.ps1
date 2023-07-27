# Parameters/arguments.
Param(
    [string]$remote = "Artifact.bin",
    [string]$local = "Artifact.bin"
)

# Variables.
$username = $env:FTP_USERNAME;
$password = $env:FTP_PASSWORD;
$ftp_server = $env:FTP_SERVER;
$localPath = (Get-Location).Path + "/" + $local;
$remotePath = "$ftp_server/files/shoko-server/$remote";
if (!$remotePath.StartsWith("ftp://")) {
    Write-Output "Adding protocol to remote path…";
    $remotePath = "ftp://$remotePath";
}

# Log inputs.
Write-Output "Starting file upload...";
Write-Output "Remote path: /files/shoko-server/$remote";
Write-Output "Local path: $localPath";

try {
    # Create the request.
    $request = [System.Net.FtpWebRequest]::Create($remotePath);
    $request.Method = [System.Net.WebRequestMethods+FTP]::UploadFile;
    $request.Credentials = New-Object System.Net.NetworkCredential($username, $password);

    # Upload the file.
    [byte[]] $bytes = [System.IO.File]::ReadAllBytes($localPath);
    [System.IO.Stream]$stream = $request.GetRequestStream();
    $stream.Write($bytes, 0, $bytes.Length);
    $stream.Close();
    $stream.Dispose();

    # Wait for an answer…
    $response = [System.Net.FtpWebResponse]$request.GetResponse();
    $result = $response.StatusDescription;
    $response.Close();

    # …and report status.
    Write-Output $result;
}
catch {
    # Write out errors.
    Write-Error $_.Exception.Message;
    throw;
}
