[CmdletBinding()]
param(
    [string]$PythonLauncher = "py",
    [string]$PythonVersion = "-3.11",
    [string]$FfmpegPath = "",
    [switch]$SkipTests,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$VenvPath = Join-Path $RepoRoot ".venv-windows"
$PythonExe = Join-Path $VenvPath "Scripts\python.exe"
$SpecPath = Join-Path $RepoRoot "windows\PixivUtil2-Windows.spec"
$DistDir = Join-Path $RepoRoot "dist\PixivUtil2-Windows"
$ZipPath = Join-Path $RepoRoot "dist\PixivUtil2-Windows.zip"

Set-Location $RepoRoot

if (-not (Test-Path $PythonExe)) {
    Write-Host "Creating Windows build virtual environment..."
    & $PythonLauncher $PythonVersion -m venv $VenvPath
}

Write-Host "Installing build dependencies..."
& $PythonExe -m pip install --upgrade pip
& $PythonExe -m pip install -r requirements.txt "pyinstaller>=6.20.0"

if (-not $SkipTests) {
    Write-Host "Running test suite..."
    & $PythonExe -m pip install pytest
    & $PythonExe -m pytest -q test
}

if ($FfmpegPath) {
    $ResolvedFfmpeg = (Resolve-Path $FfmpegPath).Path
    if (-not (Test-Path $ResolvedFfmpeg -PathType Leaf)) {
        throw "FFmpeg path does not point to a file: $ResolvedFfmpeg"
    }
    $env:PIXIVUTIL_FFMPEG = $ResolvedFfmpeg
    Write-Host "Bundling FFmpeg from $ResolvedFfmpeg"
}

Write-Host "Building native Windows bundle..."
& $PythonExe -m PyInstaller --clean --noconfirm $SpecPath

if (-not (Test-Path $DistDir)) {
    throw "Expected PyInstaller output was not created: $DistDir"
}

if (-not $NoZip) {
    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    Write-Host "Creating release archive..."
    Compress-Archive -Path (Join-Path $DistDir "*") -DestinationPath $ZipPath
    Write-Host "Release archive: $ZipPath"
}

Write-Host "Windows bundle: $DistDir"
