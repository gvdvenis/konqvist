# Check for Administrator privileges
$check = [char]10003  # Unicode check mark
$cross = [char]10007  # Unicode cross mark
$warn = [char]9888    # Unicode warning sign
$bullet = [char]8226  # Unicode bullet point

# Set output directory to obj
$outputDir = Join-Path $PSScriptRoot '..\.certs'
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # Inform user if not running as admin
    Write-Host "$warn  This script must be run as Administrator. Exiting." -ForegroundColor Yellow
    exit 1
}

# Change to output directory for all output
Set-Location -Path $outputDir

# Check for existing certs
$certExists = (Test-Path devcert.pfx) -or (Test-Path devcert.crt)
if ($certExists) {
    # Prompt user if certs already exist
    Write-Host "$warn Existing dev certificate found. Overwrite? (y/n)" -ForegroundColor Yellow
    $response = Read-Host "> "
    if ($response -ne 'y' -and $response -ne 'Y') {
        # Inform user if aborting
        Write-Host "$cross Aborting setup." -ForegroundColor Red
        exit 0
    }
}

# Gather all local IPv4 addresses (excluding loopback and APIPA)
$localIPs = Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
    Select-Object -ExpandProperty IPAddress
if (-not $localIPs -or $localIPs.Count -eq 0) {
    # Inform user if no suitable IPs found
    Write-Host "$cross No suitable local IPv4 addresses found. Aborting setup." -ForegroundColor Red
    exit 2
}

# Build alt_names section for all IPs
$altNames = "DNS.1 = localhost`nIP.1  = 127.0.0.1"
$ipIndex = 2
foreach ($ip in $localIPs) {
    $altNames += "`nIP.$ipIndex  = $ip"
    $ipIndex++
}

# Show indented list of IPs to be added
Write-Host "$check The following IP addresses will be included in the certificate:" -ForegroundColor Cyan
Write-Host "    $bullet 127.0.0.1"
foreach ($ip in $localIPs) {
    Write-Host "    $bullet $ip"
}

# Write OpenSSL config
@"
[req]
default_bits       = 2048
prompt             = no
default_md         = sha256
req_extensions     = req_ext
distinguished_name = dn

[dn]
CN = KonqVist Dev Certificate

[req_ext]
subjectAltName = @alt_names

[alt_names]
$altNames
"@ | Set-Content san.cnf

# Generate key and cert
Write-Host "$check Generating key and certificate..."
openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout devcert.key -out devcert.crt -config san.cnf -extensions req_ext > $null 2>&1

# Export to PFX
Write-Host "$check Exporting to PFX..."
openssl pkcs12 -export -out devcert.pfx -inkey devcert.key -in devcert.crt -passout pass: > $null 2>&1

# Import to Trusted Root store (Windows)
Write-Host "$check Importing certificate to Trusted Root store..."
$importedCert = Import-Certificate -FilePath .\devcert.crt -CertStoreLocation Cert:\LocalMachine\Root

# Inform user about completion
Write-Host "$check Certificate created as devcert.pfx and imported to Trusted Root store."

# Clean up intermediate files
Write-Host "$check Clean up after ourselves."
if (Test-Path san.cnf) {
    Remove-Item san.cnf -Force
}
if (Test-Path devcert.key) {
    Remove-Item devcert.key -Force
}
