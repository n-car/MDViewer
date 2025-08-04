# Script per creare release di MDViewer
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$Configuration = "Release"
)

Write-Host "Creating MDViewer release v$Version..." -ForegroundColor Green

# Build solution
msbuild MDViewer.sln /p:Configuration=$Configuration /p:Platform="Any CPU"

# Create release folder
$releaseFolder = "releases\v$Version"
New-Item -ItemType Directory -Force -Path $releaseFolder

# Create portable version
$portableFolder = "$releaseFolder\MDViewer-v$Version-Portable"
New-Item -ItemType Directory -Force -Path $portableFolder

# Copy release files
Copy-Item "MDViewer\bin\$Configuration\*" -Destination $portableFolder -Recurse

# Create ZIP
$zipPath = "$releaseFolder\MDViewer-v$Version-Portable.zip"
Compress-Archive -Path "$portableFolder\*" -DestinationPath $zipPath -Force

Write-Host "Release created: $releaseFolder"