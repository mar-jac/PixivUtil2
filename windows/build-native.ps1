[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "src\PixivUtil.Windows\PixivUtil.Windows.csproj"
$PublishDir = Join-Path $RepoRoot "artifacts\PixivUtil.Windows\$Runtime"

Set-Location $RepoRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK was not found. Install .NET 10 SDK and the Windows App SDK workload on Windows."
}

if (-not $NoRestore) {
    dotnet restore $ProjectPath -r $Runtime
}

dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:WindowsPackageType=None `
    -p:PublishDir="$PublishDir\"

Write-Host "Native Windows app published to $PublishDir"
