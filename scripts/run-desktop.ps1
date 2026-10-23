#Requires -Version 5.1
<#
.SYNOPSIS
  One-shot: build embedded web UI + run TikTok MCP Desktop (WPF) on Windows.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Need($cmd) {
  if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
    throw "Missing dependency: $cmd"
  }
}

Need "dotnet"
Need "npm"

$dotnetVersion = (& dotnet --version)
Write-Host "dotnet $dotnetVersion"
if (-not $dotnetVersion.StartsWith("8.")) {
  Write-Warning "Recommended: .NET 8 SDK. Found: $dotnetVersion"
}

Write-Host "==> Build web UI"
Push-Location "$root\web\ui"
npm install --silent
npm run build
Pop-Location

Write-Host "==> Build & run WPF desktop"
dotnet build "$root\src\TikTokMcpDesktop\TikTokMcpDesktop.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Write-Host ""
Write-Host "Launching TikTokMcpDesktop..."
Write-Host "Tip: client_id 可留空，点「授权登录」会自动 Dynamic Client Registration。"
Write-Host ""

dotnet run --project "$root\src\TikTokMcpDesktop\TikTokMcpDesktop.csproj" -c Release --no-build
