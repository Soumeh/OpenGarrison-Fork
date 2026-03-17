param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [string[]]$Platforms = @("win-x64", "linux-x64", "osx-x64", "osx-arm64"),
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $repoRoot)
$clientProjectRoot = Join-Path $repoRoot "src\OpenGarrison.Client"
$toolManifestPath = Join-Path $clientProjectRoot ".config\dotnet-tools.json"
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "dist"
}

function Get-ArchiveExtension {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Platform
    )

    switch -Wildcard ($Platform) {
        "win-*" { return ".zip" }
        default { return ".tar.gz" }
    }
}

function New-Archive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    if (Test-Path $DestinationPath) {
        Remove-Item $DestinationPath -Force
    }

    if ($Platform -like "win-*") {
        Compress-Archive -Path $SourceDirectory -DestinationPath $DestinationPath -Force
        return
    }

    $parent = Split-Path -Parent $SourceDirectory
    $leaf = Split-Path -Leaf $SourceDirectory
    & tar -czf $DestinationPath -C $parent $leaf
    if ($LASTEXITCODE -ne 0) {
        throw "tar packaging failed for $Platform."
    }
}

function New-UnixLaunchScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$ExecutableName
    )

    $scriptContents = @"
#!/bin/sh
set -eu
SCRIPT_DIR=`$(CDPATH= cd -- "`$(dirname -- "`$0")" && pwd)
chmod +x "`$SCRIPT_DIR/$ExecutableName"
exec "`$SCRIPT_DIR/$ExecutableName" "`$@"
"@

    $scriptContents = $scriptContents -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText(
        $Path,
        $scriptContents,
        [System.Text.UTF8Encoding]::new($false))
}

function Copy-MinimalModernAssets {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceRoot,
        [Parameter(Mandatory = $true)]
        [string]$StagingRoot
    )

    $modernAssetDirectoryName = "GML-OpenGarrison-Modern"
    $requiredEntries = @(
        @{ Source = "$modernAssetDirectoryName\Sprites\OpenGarrisonFontS.xml"; Target = "Assets\$modernAssetDirectoryName\Sprites\OpenGarrisonFontS.xml" },
        @{ Source = "$modernAssetDirectoryName\Sprites\OpenGarrisonFontS.images"; Target = "Assets\$modernAssetDirectoryName\Sprites\OpenGarrisonFontS.images" }
    )

    foreach ($entry in $requiredEntries) {
        $sourcePath = Join-Path $WorkspaceRoot $entry.Source
        $targetPath = Join-Path $StagingRoot $entry.Target
        if (-not (Test-Path $sourcePath)) {
            throw "Required $modernAssetDirectoryName packaging source is missing: $sourcePath"
        }

        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
        Copy-Item $sourcePath $targetPath -Recurse -Force
    }
}

function Remove-LinuxBundledOpenAl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$StagingRoot
    )

    if ($Platform -notlike "linux-*") {
        return
    }

    $bundledOpenAlPath = Join-Path $StagingRoot "libopenal.so"
    if (Test-Path $bundledOpenAlPath) {
        Remove-Item $bundledOpenAlPath -Force
    }
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$packageArtifacts = @()
$restorePlatforms = $Platforms | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
if ($restorePlatforms.Count -eq 0) {
    throw "At least one platform must be specified."
}

Push-Location $repoRoot
try {
    if (Test-Path $toolManifestPath) {
        Write-Host "Restoring local dotnet tools from client manifest..."
        Push-Location $clientProjectRoot
        try {
            dotnet tool restore
            if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Host "No local dotnet tool manifest found; skipping dotnet tool restore."
    }

    if (-not $SkipTests) {
        Write-Host "Restoring solution for test run..."
        dotnet restore OpenGarrison.sln
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

        Write-Host "Running release tests..."
        dotnet test OpenGarrison.sln -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet test failed." }
    }

    foreach ($platform in $Platforms) {
        $packageName = "opengarrison-$platform"
        $stagingRoot = Join-Path $OutputRoot $packageName
        $archivePath = Join-Path $OutputRoot ($packageName + (Get-ArchiveExtension -Platform $platform))

        if (Test-Path $stagingRoot) {
            Remove-Item $stagingRoot -Recurse -Force
        }

        New-Item -ItemType Directory -Path $stagingRoot | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $stagingRoot "Assets") | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $stagingRoot "config") | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $stagingRoot "Maps") | Out-Null

        Write-Host "Restoring solution for runtime $platform..."
        dotnet restore OpenGarrison.sln -r $platform
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for $platform." }

        Write-Host "Publishing packages for $platform..."
        dotnet publish .\src\OpenGarrison.Client\OpenGarrison.Client.csproj -c $Configuration -r $platform --self-contained true --no-restore -o $stagingRoot
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish for OpenGarrison.Client failed on $platform." }
        dotnet publish .\src\OpenGarrison.ServerLauncher\OpenGarrison.ServerLauncher.csproj -c $Configuration -r $platform --self-contained true --no-restore -o $stagingRoot
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish for OpenGarrison.ServerLauncher failed on $platform." }
        dotnet publish .\src\OpenGarrison.Server\OpenGarrison.Server.csproj -c $Configuration -r $platform --self-contained true --no-restore -o $stagingRoot
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish for OpenGarrison.Server failed on $platform." }

        Remove-LinuxBundledOpenAl -Platform $platform -StagingRoot $stagingRoot

        New-Item -ItemType Directory -Force -Path (Join-Path $stagingRoot "Assets") | Out-Null
        New-Item -ItemType Directory -Force -Path (Join-Path $stagingRoot "config") | Out-Null
        New-Item -ItemType Directory -Force -Path (Join-Path $stagingRoot "Maps") | Out-Null

        $contentOutput = Join-Path $repoRoot "src\OpenGarrison.Client\Content\bin\DesktopGL\Content"
        if (Test-Path $contentOutput) {
            Copy-Item $contentOutput (Join-Path $stagingRoot "Content") -Recurse -Force
        }

        $assetCopies = @(
            @{ Source = "Source\OpenGarrison"; Target = "Assets\Source\OpenGarrison" },
            @{ Source = "Source\OpenGarrison.CSharp\src\OpenGarrison.Backgrounds"; Target = "Assets\Source\OpenGarrison.CSharp\src\OpenGarrison.Backgrounds" },
            @{ Source = "Music"; Target = "Assets\Music" }
        )

        foreach ($copy in $assetCopies) {
            $sourcePath = Join-Path $workspaceRoot $copy.Source
            $targetPath = Join-Path $stagingRoot $copy.Target
            $targetParent = Split-Path -Parent $targetPath
            if (-not (Test-Path $sourcePath)) {
                throw "Required packaging source is missing: $sourcePath"
            }

            New-Item -ItemType Directory -Force -Path $targetParent | Out-Null
            Copy-Item $sourcePath $targetPath -Recurse -Force
        }

        Copy-MinimalModernAssets -WorkspaceRoot $workspaceRoot -StagingRoot $stagingRoot

        Copy-Item (Join-Path $repoRoot "packaging\config\client.settings.json") (Join-Path $stagingRoot "config\client.settings.json") -Force
        Copy-Item (Join-Path $repoRoot "packaging\config\input.bindings.json") (Join-Path $stagingRoot "config\input.bindings.json") -Force
        Copy-Item (Join-Path $repoRoot "packaging\config\server.settings.json") (Join-Path $stagingRoot "config\server.settings.json") -Force
        Copy-Item (Join-Path $workspaceRoot "sampleMapRotation.txt") (Join-Path $stagingRoot "config\sampleMapRotation.txt") -Force
        Copy-Item (Join-Path $repoRoot "packaging\README.txt") (Join-Path $stagingRoot "README.txt") -Force
        Copy-Item (Join-Path $workspaceRoot "GPL.txt") (Join-Path $stagingRoot "LICENSE.txt") -Force

        if ($platform -notlike "win-*") {
            New-UnixLaunchScript -Path (Join-Path $stagingRoot "run-client.sh") -ExecutableName "OpenGarrison.Client"
            New-UnixLaunchScript -Path (Join-Path $stagingRoot "run-server.sh") -ExecutableName "OpenGarrison.Server"
            New-UnixLaunchScript -Path (Join-Path $stagingRoot "run-server-launcher.sh") -ExecutableName "OpenGarrison.ServerLauncher"
        }

        New-Archive -Platform $platform -SourceDirectory $stagingRoot -DestinationPath $archivePath
        $packageArtifacts += [pscustomobject]@{
            Platform = $platform
            Folder = $stagingRoot
            Archive = $archivePath
        }
    }
}
finally {
    Pop-Location
}

Write-Host "Packages ready:"
foreach ($artifact in $packageArtifacts) {
    Write-Host "  $($artifact.Platform)"
    Write-Host "    Folder:  $($artifact.Folder)"
    Write-Host "    Archive: $($artifact.Archive)"
}
