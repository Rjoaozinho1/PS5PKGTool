#!/usr/bin/env bash
# Runs the CLI test suite in a Linux .NET SDK container, then smoke-tests the published binary in a
# bare Debian container (no .NET, no ICU). Needs Docker (colima works) and the .NET 10 SDK on the host.
set -euo pipefail
cd "$(dirname "$0")"

platform_for() {
  case "$1" in
    linux-arm64) echo linux/arm64 ;;
    linux-x64) echo linux/amd64 ;;
  esac
}

case "$(docker info --format '{{.Architecture}}')" in
  aarch64 | arm64) native=linux-arm64 other=linux-x64 ;;
  x86_64 | amd64) native=linux-x64 other=linux-arm64 ;;
  *) echo "unsupported Docker architecture" >&2; exit 1 ;;
esac

echo "== Test suite on Linux (mcr.microsoft.com/dotnet/sdk:10.0)"
# Copy the sources without bin/obj so the host's macOS build output is not reused inside Linux.
docker run --rm -v "$PWD":/src:ro mcr.microsoft.com/dotnet/sdk:10.0 bash -euo pipefail -c '
  mkdir /work
  tar -C /src --exclude=./.git --exclude=./dist --exclude="*/bin" --exclude="*/obj" -cf - . | tar -C /work -xf -
  cd /work
  dotnet test PS5PKGTool.Cli.Tests/PS5PKGTool.Cli.Tests.csproj -c Release
'

./build-linux.sh "$native" "$other"

smoke() {
  local rid=$1
  echo "== Smoke test: dist/$rid/ps5pkg in debian:stable-slim ($(platform_for "$rid"))"
  docker run --rm -i --platform "$(platform_for "$rid")" -v "$PWD/dist/$rid":/opt/ps5pkg:ro debian:stable-slim \
    bash -euo pipefail <<'SMOKE'
/opt/ps5pkg/ps5pkg --version
mkdir -p /tmp/dump/sce_sys
cat > /tmp/dump/sce_sys/param.json <<'JSON'
{
  "titleId": "PPSA99999",
  "contentId": "UP9999-PPSA99999_00-CLITESTFIXTURE00",
  "contentVersion": "01.000.000",
  "localizedParameters": { "defaultLanguage": "en-US", "en-US": { "titleName": "CLI Fixture" } }
}
JSON
head -c 65536 /dev/urandom > /tmp/dump/eboot.bin
/opt/ps5pkg/ps5pkg convert /tmp/dump -o /tmp/smoke.ffpfsc --quiet
/opt/ps5pkg/ps5pkg info /tmp/smoke.ffpfsc | tee /tmp/info.txt
grep -q 'PPSA99999' /tmp/info.txt
echo "smoke test passed"
SMOKE
}

smoke "$native"
if docker run --rm --platform "$(platform_for "$other")" debian:stable-slim true > /dev/null 2>&1; then
  smoke "$other"
else
  echo "== Skipping the $other smoke test: this Docker host cannot run $(platform_for "$other") containers"
fi
