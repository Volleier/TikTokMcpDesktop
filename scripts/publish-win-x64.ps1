#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "==> Building web UI"
Push-Location "$root\web\ui"
npm install
npm run build
Pop-Location

$out = "$root\publish\win-x64"
New-Item -ItemType Directory -Force -Path $out | Out-Null
New-Item -ItemType Directory -Force -Path "$out\web\ui" | Out-Null
Copy-Item -Recurse -Force "$root\web\ui\dist" "$out\web\ui\dist"

Write-Host "==> Publishing WPF (self-contained win-x64)"
dotnet publish "$root\src\TikTokMcpDesktop\TikTokMcpDesktop.csproj" `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $out

Write-Host "Done: $out\TikTokMcpDesktop.exe"
Write-Host "Requires WebView2 Runtime: https://developer.microsoft.com/microsoft-edge/webview2/"
