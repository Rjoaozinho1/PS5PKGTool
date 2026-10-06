# Linux CLI (`ps5pkg`) — Design

Date: 2026-10-06
Branch: `feat/linux-cli` (fork `Rjoaozinho1/PS5PKGTool`)
Status: approved in conversation, pending written-spec review

## 1. Goal

Ship a command-line tool, `ps5pkg`, that runs on Linux as a single self-contained executable
with nothing to install, and exposes the image creation/conversion and metadata features of
PS5 PKG Tool.

Success means:

- On a clean Linux machine with no .NET and no ICU installed, `./ps5pkg convert <dump> -o game.ffpfsc`
  produces the image the Windows app would produce with its default settings.
- `ps5pkg info` and `ps5pkg scan` show the same metadata the GUI's library and Overview show.
- The test suite passes on macOS and inside a Linux container.

## 2. Context and decisions

| Decision | Choice | Why |
|---|---|---|
| Language | C# / .NET 10, not Go | The user's goal is an easy single binary. A self-contained .NET single-file build delivers that while reusing `PS5PKGTool.Core`, `PS5PKGTool.Ffpfsc`, `PS5PKGTool.Ufs2` and the vendored `ProsperoPkgTool.dll` unchanged. A Go version would have to reimplement those libraries, and FPKG building would also need the package engine reimplemented. `ProsperoPkgTool.dll`'s source isn't public, and LibProsperoPkg would have to be ported. |
| v1 scope | `convert` (create + convert images) and `info` / `scan` | Chosen by the user. These paths are fully managed code: no LibProsperoPkg, Magick.NET, OpenSSL or Oodle. |
| Repository | Fork only | Free to make small changes in `Core`/`Ffpfsc`; no upstream PR planned. |
| Distribution | Local build script, no CI | Chosen by the user. Cross-publishing to Linux from macOS works because v1 is all managed code. |
| Argument parsing | Hand-rolled | Chosen by the user. Kept small and fully unit-tested. |

### Deferred (not in v1)

- FPKG building, package/image extraction, package/image verification commands.
- exFAT/FFPKG editing, exFAT repair, AMPR refresh, FFPKG rebuild.
- Fix for `LppBackend.MirrorTree`'s `kernel32!CreateHardLink` P/Invoke
  (`PS5PKGTool.Core/Backends/LppBackend.cs:280,296`). On Linux it throws `DllNotFoundException`, which
  the `catch` at `LppBackend.cs:263` does not handle. Only reached by FPKG builds with a DRM override,
  so it belongs to the FPKG milestone.
- Trimming / NativeAOT, CI, release automation.

## 3. Architecture

### 3.1 Projects

```
PS5PKGTool.Cli/            new  console app, net10.0, AssemblyName "ps5pkg"
  Program.cs                    entry point: Ctrl+C wiring, calls Cli.RunAsync, sets exit code
  Cli.cs                        RunAsync(args, stdout, stderr, isErrorTerminal, token) -> exit code; routing, global flags
  Cli/ArgParser.cs              tokenizer + validation against an option spec
  Cli/OptionSpec.cs             option declarations (name, alias, takes value, allowed values / range)
  Cli/UsageException.cs
  Commands/InfoCommand.cs
  Commands/ScanCommand.cs
  Commands/ConvertCommand.cs
  Output/TableWriter.cs
  Output/JsonOutput.cs
  Output/ProgressReporter.cs
  Output/SizeFormat.cs
PS5PKGTool.Cli.Tests/      new  xUnit, net10.0, references PS5PKGTool.Cli
build-linux.sh             new  publishes linux-x64 and linux-arm64 into dist/<rid>/ps5pkg
test-linux.sh              new  runs the test suite and the binary smoke test in Linux containers
```

`PS5PKGTool.Cli` references `PS5PKGTool.Core`, `PS5PKGTool.Ffpfsc` and `PS5PKGTool.Ufs2`.
`ProsperoPkgTool.dll` flows in through Core's existing `<Reference>` (`Private=true`). The CLI never
references the WinForms project, DarkUI, `LibProsperoPkg12` or the Oodle DLL.

Both new projects are added to `PS5PKGTool.slnx`. The scripts build the CLI project directly, so the
WinForms project (and its out-of-repo DarkUI reference) never has to build on macOS or Linux.

### 3.2 Publish settings (`PS5PKGTool.Cli.csproj`)

- `SelfContained=true`, `PublishSingleFile=true`, `EnableCompressionInSingleFile=true`
  (expected size about 35 MB).
- `InvariantGlobalization=true`. Without it, .NET on Linux needs libicu, which minimal distros and
  containers often lack.
- No trimming in v1: `ProsperoPkgTool.dll` and the `System.Text.Json` output rely on reflection.
- `Version` 0.1.0, independent of the GUI's version.

### 3.3 Changes to existing code

1. **Remove the unused `Oodle.NET` package reference** from `PS5PKGTool.Core/PS5PKGTool.Core.csproj`.
   No source file references it. `ProsperoPkgTool.dll` has its own managed Kraken codec.
2. **Add an optional temp-directory parameter** (`string? tempDirectory = null`, appended last) to:
   - `Ps5ImageConversionService.ConvertAsync` (`PS5PKGTool.Ffpfsc/ImageConversionService.cs`);
   - `SonyPackageImageConversion.ConvertAsync` (`PS5PKGTool.Core/Services/SonyPackageImageConversion.cs`),
     which also forwards it to `Ps5ImageConversionService.ConvertAsync`.

   When set, the per-run folder is created directly under it as `<tempDirectory>/ps5pkg-<guid>` and
   deleted in the existing `finally`, so the given directory ends up exactly as it was. With `null` the
   current `Path.GetTempPath()/PS5PKGTool/<kind>/<guid>` layout is unchanged, so the GUI is unaffected.
   Reason: image→image and `.pkg`→image conversions extract the whole game tree to temp first, and on
   many Linux distros `/tmp` is a RAM-backed tmpfs.
3. `.gitignore`: `docs/` → `docs/*` + `!docs/superpowers/` (done with this spec), plus `dist/`.

### 3.4 Build script

`build-linux.sh` runs, for each RID in `linux-x64 linux-arm64`:

```
dotnet publish PS5PKGTool.Cli/PS5PKGTool.Cli.csproj -c Release -r <rid> -o dist/<rid>
```

It prints the resulting paths and sizes and fails fast (`set -euo pipefail`). It works from macOS
or Linux with the .NET 10 SDK installed.

## 4. Command reference

```
ps5pkg info <path> [--json]
ps5pkg scan <folder>... [--no-recurse] [--json]
ps5pkg convert <source> -o <output> [--to exfat|ffpkg|ffpfsc] [--force] [--temp <dir>] [format options]
ps5pkg --help | --version | <command> --help
```

Global flags, accepted anywhere: `-h/--help`, `--version`, `--quiet` (no progress), `--debug`
(stack traces on errors).

### 4.1 `info <path>`

- `<path>`: a dump folder (containing `sce_sys/param.json`), `param.json`, `eboot.bin`, `.pkg`,
  `.exfat`, `.ffpkg` or `.ffpfsc`.
- Implementation: `Ps5LibraryScanner.ScanAsync([path], recursive: false)`. The locator already handles
  single files and dump roots.
- 0 titles found → `error: no PS5 title found at <path>`, exit 1.
  More than one → `error: found N titles at <path>; use 'ps5pkg scan'`, exit 1.
- Text output, one `Key: value` line each: Title, Title ID, Content ID, Version (`DisplayVersion`),
  Category (`ApplicationCategory`), Required firmware (`RequiredSystemSoftware`), SDK (`SdkVersion`),
  DRM type (`DrmType`), Format (`SourceDescription`), Size (`SourceSize`, human-readable), Path
  (`RootPath`). Empty values are shown as `-`.
- `--json`: the full `Ps5GameInfo` record (§5.4 format).

### 4.2 `scan <folder>...`

- One or more folders. Searches subfolders by default; `--no-recurse` turns that off.
- Text output: a table with columns Title ID │ Version │ Category │ Format │ Size │ Title │ Path, sorted
  by Title ID, then Path.
- `--json`: a JSON array of `Ps5GameInfo` records.
- Scan errors (`Ps5ScanResult.Errors`) are printed to stderr as `warning: <message>`. The results are
  still printed, and the exit code is 1 when there were any errors (0 otherwise).
- Progress (`Ps5ScanProgress`) goes to stderr per §5.3.

### 4.3 `convert <source> -o <output>`

**Routing**

| Source | Call |
|---|---|
| `.pkg` file | `SonyPackageImageConversion.ConvertAsync` |
| dump folder, `.exfat`, `.ffpkg`, `.ffpfsc` | `Ps5ImageConversionService.ConvertAsync` |

These are the same two calls the GUI's `MainForm.ConvertImageAsync` makes
(`PS5PKGTool/Forms/MainForm.ImageTools.cs:595`).

**Target:** `--to exfat|ffpkg|ffpfsc`. Otherwise it's inferred from the output extension (`.exfat`,
`.ffpkg`, `.ffpfsc`, case-insensitive). If neither gives a target, that's a usage error (exit 2).
If `--to` and the extension disagree, `--to` wins and a `warning:` line is printed.

**Common options**

- `-o, --output <path>`: required.
- `--force`: overwrite an existing output (`overwrite: true`).
- `--temp <dir>`: base folder for extraction (§3.3). Always accepted; only used when the source is a
  `.pkg` or an image that must be extracted. When extraction will happen, a notice is printed to
  stderr: `note: extracting to <dir>`.

**Format options** (defaults match the GUI's Image Tools defaults, not the option-class defaults)

| Option | Values | Default | Builds | Allowed targets |
|---|---|---|---|---|
| `--cluster` | `auto`, `32k`, `64k` | `auto` | `ExfatBuildOptions.ClusterSize` = null / 32768 / 65536 | exfat, ffpfsc |
| `--no-ampr` | flag | AMPR on | `ExfatBuildOptions.GenerateAmprIndex = false` | exfat, ffpfsc |
| `--level` | 1–9 | 7 | `PfscCompressionOptions.CompressionLevel` | ffpfsc |
| `--min-gain` | 0–100 | 1 | `PfscCompressionOptions.MinimumGainPercent` | ffpfsc |
| `--block` | `32k`, `64k` | `32k` | `FfpkgBuildOptions.BlockSize` | ffpkg |
| `--fragment` | `4k`, `64k` | `4k` | `FfpkgBuildOptions.FragmentSize`, capped at block size | ffpkg |
| `--inode-density` | `256k`, `512k`, `1m` | `256k` | `FfpkgBuildOptions.BytesPerInode` = 262144 / 524288 / 1048576 | ffpkg |
| `--min-free` | 0–50 | 0 | `FfpkgBuildOptions.MinFreePercent` | ffpkg |

Rules:

- An option not allowed for the chosen target → usage error (exit 2), for example
  `error: --level only applies to --to ffpfsc`.
- `--cluster` / `--no-ampr` with an `.exfat` or `.ffpkg` **source** and target `ffpfsc` → usage error.
  That path wraps the existing image as-is (`ImageConversionService.cs:83`), so the options would have
  no effect.
- `--fragment 64k` with `--block 32k` → fragment capped to 32k (the GUI behaves the same way), with a
  `warning:` line.
- Option objects passed to the library mirror the GUI. `ExfatBuildOptions` is passed for exfat and
  ffpfsc targets. `FfpfscBuildOptions { Compression = … }` is passed for ffpfsc. `FfpkgBuildOptions`
  is passed for ffpkg. All others are `null`.

**Output:** on success, one stdout line: `<output path>  <format>  <size>  (<file count> files)` from
`Ps5ImageConversionResult`. `--json` is not offered on `convert` in v1.

## 5. Internals

### 5.1 Entry and routing

- `Program.Main` creates a `CancellationTokenSource` and hooks `Console.CancelKeyPress`. The first
  Ctrl+C sets `e.Cancel = true` and cancels the token. A second Ctrl+C lets the process terminate.
  `Main` then calls `Cli.RunAsync(args, Console.Out, Console.Error, isErrorTerminal: !Console.IsErrorRedirected, token)`
  and returns its exit code.
- `Cli.RunAsync` pulls out the global flags, picks the command by its first positional argument, and
  catches exceptions (§5.5). Tests call it directly.

### 5.2 Argument parser

- Input: `string[]` plus the command's `OptionSpec[]`. Output: `ParsedArgs` (positionals, a
  name→value map, a set of flags).
- Supported forms: `--name value`, `--name=value`, `-o value`, `--flag`, and `--` (everything after it
  is positional). The spec defines short aliases (`-o`, `-h`).
- Validation: unknown option, missing value, a repeated single-value option, a value outside its
  allowed set or integer range, or a missing required positional → `UsageException` with a one-line
  message.
- Help text for each command lives next to its spec. Global `--help` lists the commands.

### 5.3 Progress

`ProgressReporter` implements `IProgress<Ps5ImageConversionProgress>` and `IProgress<Ps5ScanProgress>`
and writes only to stderr.

- **Terminal** (`isErrorTerminal`): a single line rewritten with `\r`, for example
  `Building image  42%  (1.2 / 2.8 GB)`, throttled to about 10 updates/s. A final newline when done or
  on error.
- **Redirected:** one line per stage change; no percentages and no `\r`.
- `--quiet`: nothing.

### 5.4 Output formats

- Sizes are 1024-based with one decimal (`512 B`, `12.3 KB`, `4.0 GB`).
- JSON: `System.Text.Json`, camelCase property names, enums as strings, indented, UTF-8. Raw byte
  counts are left as numbers.
- Tables: column widths come from the content; the Title column is truncated with `…` at 40
  characters. Path is never truncated.

### 5.5 Errors and exit codes

| Situation | stderr | Exit |
|---|---|---|
| Success | — | 0 |
| `UsageException` | `error: <message>` + `run 'ps5pkg <command> --help'` | 2 |
| `IOException`, `InvalidDataException`, `FileNotFoundException`, `DirectoryNotFoundException`, `UnauthorizedAccessException`, `NotSupportedException` | `error: <message>` | 1 |
| Any other exception | `error: <message>` (stack trace with `--debug`) | 1 |
| `OperationCanceledException` after Ctrl+C | `cancelled` | 130 |
| `scan` finished with per-path errors | `warning:` lines | 1 |

Cleanup on failure or cancel is done by the library. `Ps5ImageConversionService` writes to
`<output>.<guid>.tmp` and deletes it and its temp tree in `finally`. `SonyPackageImageConversion`
deletes its temp tree in `finally`. The CLI adds no cleanup of its own.

## 6. Testing

### 6.1 Fixture

A `TestDump` helper builds a synthetic dump in a per-test temp folder:
- `sce_sys/param.json` with minimal valid fields: title ID `PPSA99999`, a content ID, an English
  `titleName` under `localizedParameters`, content version, application category, required firmware;
- a dummy `eboot.bin`;
- a few nested files, some larger than a PFSC block so compression paths run.

No real game data is committed.

### 6.2 Unit tests

- Parser: positionals, `--x=y`, `--x y`, `-o`, flags, `--`, unknown option, missing value, repeated
  option, out-of-range integer, value not in the allowed set.
- `convert` option rules: target from extension, `--to` overriding the extension (with a warning), no
  target → exit 2, an option that doesn't match the target → exit 2, cluster options with a wrap path →
  exit 2, fragment capped at block (with a warning).
- **GUI-default parity:** with no format options, the options passed to the library equal the GUI
  defaults: exFAT cluster `null` + AMPR on; FFPFSC level 7 / minimum gain 1; FFPKG 32768 / 4096 /
  262144 / 0. To allow this, `ConvertCommand` builds its options through a pure function
  (`BuildOptions(target, parsed)`) that tests call directly.
- Size formatting and table layout.

### 6.3 Integration tests (real library code)

1. `info <fixture>` → exit 0, output contains `PPSA99999`.
2. `convert <fixture> -o x.exfat`, `x.ffpkg`, `x.ffpfsc` → each exits 0; `info` on each output shows
   `PPSA99999`.
3. `convert x.exfat -o y.ffpkg --temp <test temp>` → exit 0, and the test temp folder is empty afterwards.
4. `scan <folder> --json` → finds the dump plus the three images (4 records).
5. An existing output without `--force` → exit 1 and the file is unchanged; with `--force` → exit 0.
6. `convert <fixture> -o x.exfat --level 5` → exit 2.
7. `convert` with an already-cancelled token → exit 130, and no `*.tmp` files in the output folder.
8. `.pkg` → image: the test setup builds a debug package from the fixture with the in-repo managed
   builder (`ProsperoDebugPackageBuilder.CreateFromDirectoryAsync`, default passcode), then runs
   `convert pkg -o z.exfat` and `info z.exfat`. If the engine rejects a synthetic dump, this test is
   replaced by a documented manual check against a real debug package, and this spec is updated to
   say so.

### 6.4 Where tests run

- **macOS:** `dotnet test PS5PKGTool.Cli.Tests` for the inner loop.
- **Linux:** `test-linux.sh` runs the suite inside `mcr.microsoft.com/dotnet/sdk:10.0` (colima,
  linux/arm64 on this machine). This catches case-sensitivity bugs that macOS's case-insensitive file
  system hides.
- **Binary smoke test:** `test-linux.sh` also runs `dist/linux-arm64/ps5pkg` in `debian:stable-slim`
  (no .NET, no ICU): `--version`, `convert <fixture> -o t.ffpfsc`, `info t.ffpfsc`. The fixture is
  generated by a tiny script, not the test project. The linux-x64 smoke test runs only if the
  container runtime can emulate x86-64; otherwise it's skipped with a message.

## 7. Prerequisites and risks

- **The .NET 10 SDK must be installed on the Mac** (the user will do this). Implementation and all
  testing wait on it.
- `test-linux.sh` pulls `mcr.microsoft.com/dotnet/sdk:10.0` and `debian:stable-slim` on first run.
- `ProsperoPkgTool.dll` contains a `ProsperoPkgTool.Native` namespace (`RunNativeDecrypt`,
  `RunNativePackageEntries`) with no P/Invoke signatures visible. It's assumed to be unused by the
  conversion paths; integration test 8 and the Linux smoke test will show otherwise.
- `ProsperoPkgTool.dll` targets `net10.0` and was only ever run on Windows. Any Windows assumption
  inside it shows up in the Linux test run, not before.
