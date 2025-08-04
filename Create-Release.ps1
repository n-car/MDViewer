param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$Configuration = "Release"
)

Write-Host "Creating MDViewer release v$Version..." -ForegroundColor Green

# Build solution
msbuild MDViewer.sln /p:Configuration=$Configuration /p:Platform="Any CPU"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Create release folder
$releaseFolder = "releases\v$Version"
New-Item -ItemType Directory -Force -Path $releaseFolder | Out-Null

# Create portable version
$portableFolder = "$releaseFolder\MDViewer-v$Version-Portable"
New-Item -ItemType Directory -Force -Path $portableFolder | Out-Null

# Copy essential files only
$files = @(
    "MDViewer\bin\$Configuration\MDViewer.exe",
    "MDViewer\bin\$Configuration\MDViewer.exe.config",
    "MDViewer\bin\$Configuration\Microsoft.Web.WebView2.*.dll"
)

foreach ($pattern in $files) {
    Get-ChildItem $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item $_.FullName -Destination $portableFolder
        Write-Host "Copied: $($_.Name)" -ForegroundColor Gray
    }
}

# Copy localization folders
@("it", "en", "it-IT") | ForEach-Object {
    $locPath = "MDViewer\bin\$Configuration\$_"
    if (Test-Path $locPath) {
        Copy-Item $locPath -Destination $portableFolder -Recurse
        Write-Host "Copied localization: $_" -ForegroundColor Gray
    }
}

# Create ZIP
$zipPath = "$releaseFolder\MDViewer-v$Version-Portable.zip"
Compress-Archive -Path "$portableFolder\*" -DestinationPath $zipPath -Force
Write-Host "Created: $zipPath" -ForegroundColor Green

# Build setup if exists
if (Test-Path "MDViewer.Setup") {
    Write-Host "Building setup project..." -ForegroundColor Yellow
    $setupFiles = Get-ChildItem "MDViewer.Setup\*.vdproj", "MDViewer.Setup\*.wixproj" -ErrorAction SilentlyContinue
    
    if ($setupFiles) {
        $setupFile = $setupFiles[0]
        msbuild $setupFile.FullName /p:Configuration=$Configuration
        
        if ($LASTEXITCODE -eq 0) {
            # Find and copy MSI
            $msiFiles = Get-ChildItem -Path . -Name "*.msi" -Recurse | 
                        Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-5) }
            
            if ($msiFiles) {
                $destMsi = "$releaseFolder\MDViewer-v$Version-Setup.msi"
                Copy-Item $msiFiles[0].FullName -Destination $destMsi
                Write-Host "Created: $destMsi" -ForegroundColor Green
            }
        }
    }
}

# Create README
@"
# MDViewer v$Version

## Downloads:
- MDViewer-v$Version-Portable.zip: Portable version
- MDViewer-v$Version-Setup.msi: Installer (if available)

## System Requirements:
- Windows 10+, .NET Framework 4.8, WebView2 Runtime

## Usage:
- Extract ZIP and run MDViewer.exe (portable)
- Run MSI installer (installer version)
- Drag .md files or use Open button

Build: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@ | Out-File "$releaseFolder\README.txt" -Encoding UTF8

Write-Host ""
Write-Host "Release v$Version created!" -ForegroundColor Green
Write-Host "Location: $releaseFolder" -ForegroundColor Cyan
Get-ChildItem $releaseFolder | ForEach-Object { 
    Write-Host "  $($_.Name)" -ForegroundColor Gray 
}