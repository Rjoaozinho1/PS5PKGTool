# ps5pkg — PS5 PKG Tool on the command line

A single-file Linux executable with three commands. It uses the same libraries as the Windows app,
so images built with default options match what the app's Image Tools produce.

## Build

Needs the .NET 10 SDK (macOS or Linux):

```bash
./build-linux.sh                # dist/linux-x64/ps5pkg and dist/linux-arm64/ps5pkg
./build-linux.sh linux-x64      # one architecture
```

Copy `dist/<rid>/ps5pkg` to the Linux machine. Nothing else needs to be installed.

## Use

```bash
ps5pkg info  <dump folder | .pkg | .exfat | .ffpkg | .ffpfsc> [--json]
ps5pkg scan  <folder>... [--no-recurse] [--json]
ps5pkg convert <source> -o <output> [--to exfat|ffpkg|ffpfsc] [--force] [--temp <dir>] [format options]
ps5pkg <command> --help
```

Examples:

```bash
ps5pkg convert ~/dumps/PPSA01234-app -o ~/images/game.ffpfsc
ps5pkg convert game.exfat -o game.ffpkg --temp /mnt/big/tmp
ps5pkg scan ~/dumps ~/images --json > library.json
```

- Converting from an image or a `.pkg` first extracts the whole game. On many distros `/tmp` is in
  RAM, so pass `--temp` with a folder on a large disk.
- Results go to stdout. Progress, warnings and errors go to stderr. `--quiet` hides progress.
- Exit codes: `0` success, `1` failure, `2` bad usage, `130` cancelled (Ctrl+C).

## Not in this version

Building FPKGs, extracting, verifying, and editing images are only in the Windows app for now.

## Tests

```bash
dotnet test PS5PKGTool.Cli.Tests       # macOS / Linux
./test-linux.sh                         # Linux container + bare-Debian smoke test (needs Docker)
```
