Param(
    [string]$remote = "Artifact.bin",
    [string]$local = "Artifact.bin"
)

# Variables.
$ssh_user = $env:SSH_USERNAME
$ssh_server = $env:SSH_SERVER
$localPath = (Get-Location).Path + "/" + $local;
$remotePath = "/var/www/files.shokoanime.com/files/shoko-server/$remote"
$batchFile = "$env:TEMP\sftp_batch.txt"

# Check inputs
if (-not (Test-Path $localPath)) {
    Write-Error "Local file '$localPath' does not exist."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($ssh_user) -or [string]::IsNullOrWhiteSpace($ssh_server)) {
    Write-Error "SSH_USERNAME or SSH_SERVER environment variables are not set."
    exit 1
}

# Log inputs.
Write-Output "Starting file upload...";
Write-Output "Remote path: /files/shoko-server/$remote";
Write-Output "Local path: $localPath";

# Create SFTP batch file
"put `"$localPath`" `"$remotePath`"" | Out-File -Encoding ASCII $batchFile

# Upload file via SFTP
& sftp.exe -i $env:USERPROFILE\.ssh\files_id_rsa -o PreferredAuthentications=publickey -b $batchFile "${ssh_user}@${ssh_server}"

# Clean up batch file
Remove-Item $batchFile

"File available at: " + ($remotePath -replace '/var/www/','https://') | Write-Host
