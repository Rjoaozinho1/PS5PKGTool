#!/usr/bin/env bash
# Builds self-contained single-file Linux binaries of the ps5pkg CLI into dist/<rid>/ps5pkg.
# Usage: ./build-linux.sh [rid...]   (default: linux-x64 linux-arm64). Needs the .NET 10 SDK.
set -euo pipefail
cd "$(dirname "$0")"

rids=("$@")
if [ ${#rids[@]} -eq 0 ]; then rids=(linux-x64 linux-arm64); fi

for rid in "${rids[@]}"; do
  rm -rf "dist/$rid"
  dotnet publish PS5PKGTool.Cli/PS5PKGTool.Cli.csproj -c Release -r "$rid" -o "dist/$rid" --nologo
  rm -f "dist/$rid"/*.pdb
  extra="$(find "dist/$rid" -type f ! -name ps5pkg)"
  if [ -n "$extra" ]; then
    echo "unexpected files next to dist/$rid/ps5pkg:" >&2
    echo "$extra" >&2
    exit 1
  fi
  echo "dist/$rid/ps5pkg  $(du -h "dist/$rid/ps5pkg" | cut -f1)"
done
