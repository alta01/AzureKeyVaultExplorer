# Based on https://janjones.me/posts/clickonce-installer-build-publish-github/.

[CmdletBinding(PositionalBinding = $false)]
param (
    [switch]$OnlyBuild = $false
)

$appName = 'VaultExplorer'
$projDir = 'Vault\Explorer'

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$workingDir = $pwd
Write-Output "Working directory: $workingDir"

# Find MSBuild.
$msBuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
    -prerelease | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($msBuildPath)) {
    throw 'MSBuild was not found. Use the GitHub Actions release workflow on windows-latest.'
}

$msBuildVersionText = (& $msBuildPath -version -nologo | Select-Object -Last 1).Trim()
Write-Output "MSBuild: $((Get-Command $msBuildPath).Path)"
Write-Output "MSBuild version: $msBuildVersionText"

$minimumSupportedVersion = [Version]'17.14.0'
[Version]$resolvedMsBuildVersion = $null
if (-not [Version]::TryParse($msBuildVersionText, [ref]$resolvedMsBuildVersion) -or
    $resolvedMsBuildVersion -lt $minimumSupportedVersion) {
    throw "MSBuild $minimumSupportedVersion+ is required for .NET 10 ClickOnce publishing. Current: '$msBuildVersionText'. Use GitHub Actions release workflow."
}

# Load current Git tag.
$tag = $(git describe --tags)
Write-Output "Tag: $tag"

# Trim tag.
$version = $tag.TrimStart('v').Split('-')[0]
Write-Output "Version: $version"

# Clean output directory.
$publishDir = 'bin/publish'
$outDir = "$projDir/$publishDir"
if (Test-Path $outDir) {
    Remove-Item -Path $outDir -Recurse
}

# Publish the application.
Push-Location $projDir
try {
    Write-Output 'Restoring:'
    dotnet restore -r win-x64
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }

    Write-Output 'Publishing:'
    & $msBuildPath /target:publish /p:PublishProfile=ClickOnceProfile `
        /p:ApplicationVersion=$version /p:Configuration=Release `
        /p:PublishDir=$publishDir /v:m
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild publish failed with exit code $LASTEXITCODE"
    }

    # Measure publish size.
    $publishSize = (Get-ChildItem -Path "$publishDir/Application Files" -Recurse |
            Measure-Object -Property Length -Sum).Sum / 1Mb
    Write-Output ('Published size: {0:N2} MB' -f $publishSize)
} finally {
    Pop-Location
}

if ($OnlyBuild) {
    Write-Output 'Build finished.'
    exit
}

# Clone `gh-pages` branch.
$ghPagesDir = 'gh-pages'
if (-Not (Test-Path $ghPagesDir)) {
    $remoteUrl = git config --get remote.origin.url
    $hasGhPages = $true
    try {
        git ls-remote --heads $remoteUrl gh-pages | Out-Null
        $head = git ls-remote --heads $remoteUrl gh-pages
        if ([string]::IsNullOrWhiteSpace($head)) {
            $hasGhPages = $false
        }
    }
    catch {
        $hasGhPages = $false
    }

    if ($hasGhPages) {
        git clone $remoteUrl -b gh-pages --depth 1 --single-branch $ghPagesDir
    }
    else {
        git clone $remoteUrl $ghPagesDir
        Push-Location $ghPagesDir
        try {
            git checkout --orphan gh-pages
            git rm -rf . | Out-Null
            Set-Content -Path index.html -Value "<html><body><h1>$appName</h1></body></html>" -Encoding UTF8
            git add index.html
            git commit -m "Initialize gh-pages branch"
            git push origin gh-pages
        }
        finally {
            Pop-Location
        }
    }
}

Push-Location $ghPagesDir
try {
    # Remove previous application files.
    Write-Output 'Removing previous files...'
    if (Test-Path 'Application Files') {
        Remove-Item -Path 'Application Files' -Recurse
    }
    if (Test-Path "$appName.application") {
        Remove-Item -Path "$appName.application"
    }

    # Copy new application files.
    Write-Output 'Copying new files...'
    Copy-Item -Path "../$outDir/Application Files", "../$outDir/$appName.application" `
        -Destination . -Recurse

    # Stage and commit.
    Write-Output 'Staging...'
    git add -A
    $pending = git status --porcelain
    if ([string]::IsNullOrWhiteSpace($pending)) {
        Write-Output 'No publish changes detected; skipping commit/push.'
    }
    else {
        Write-Output 'Committing...'
        git commit -m "update to v$version"
        git push
    }
} finally {
    Pop-Location
}
