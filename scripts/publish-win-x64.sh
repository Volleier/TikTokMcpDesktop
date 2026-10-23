#!/usr/bin/env bash
# Cross-compile notes: WPF publish requires Windows. On Linux this script builds Core/LocalApi/web only.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Building web UI"
(cd web/ui && npm install && npm run build)

echo "==> Building Core + LocalApi + DevHost"
dotnet build src/TikTokMcpDesktop.Core/TikTokMcpDesktop.Core.csproj -c Release
dotnet build src/TikTokMcpDesktop.LocalApi/TikTokMcpDesktop.LocalApi.csproj -c Release
dotnet build src/TikTokMcpDesktop.DevHost/TikTokMcpDesktop.DevHost.csproj -c Release

if [[ "$(uname -s)" == MINGW* || "$(uname -s)" == CYGWIN* || "$(uname -s)" == MSYS* || -n "${WINDIR:-}" ]]; then
  echo "==> Publishing WPF win-x64"
  OUT="$ROOT/publish/win-x64"
  mkdir -p "$OUT/web/ui"
  cp -R web/ui/dist "$OUT/web/ui/dist"
  dotnet publish src/TikTokMcpDesktop/TikTokMcpDesktop.csproj \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT"
  echo "Done: $OUT/TikTokMcpDesktop.exe"
else
  echo "WPF publish skipped (not Windows). Use scripts/publish-win-x64.ps1 on a Windows machine."
fi
