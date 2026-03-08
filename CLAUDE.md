# BBA Native Libraries

## Purpose

This repo builds **native platform libraries** (`.dylib`, `.so`, `.dll`) from Edward Piwowar's
managed .NET EPBot bridge bidding engine DLL using **NativeAOT** compilation. The output is a
self-contained native library with no .NET runtime dependency — it can be linked directly from
Rust, C, C++, or any language that supports C FFI.

## How It Works

1. **Input**: Edward's managed .NET DLL (e.g. `EPBot8739.dll`) — a standard .NET assembly
   containing the EPBot bridge bidding engine. No source code required.

2. **FFI Layer** (`src/EPBotFFI.cs`): A thin C FFI wrapper using `[UnmanagedCallersOnly]` that
   exposes every public EPBot method as a C-callable function. This is a **1:1 thin mapping** —
   each FFI function directly calls the corresponding EPBot method with minimal transformation
   (just marshalling strings between C `char*` and .NET `string`).

3. **NativeAOT Compilation**: `dotnet publish` with `<PublishAot>true</PublishAot>` compiles
   everything (EPBot IL + FFI wrapper + .NET runtime) into a single native library.

## Architecture Decisions

### Thin FFI (not thick)
The FFI layer does NOT orchestrate multi-step workflows (like managing 4-player tables or
running full auctions). It maps 1:1 to Edward's public API. The calling application (e.g.
BBA-CLI) is responsible for orchestration. This keeps the FFI:
- Easy to maintain when Edward adds/changes methods
- Decoupled from any specific caller's workflow
- A faithful mirror of Edward's API

### Namespace Auto-Detection
Edward versions his DLL namespace (EPBot8739, EPBot8740, etc.). The build script automatically
detects the namespace from the DLL and generates a `global using` alias so `EPBotFFI.cs` always
references `EPBot` regardless of the actual namespace version.

### No ModuleNET.cs Needed
Edward's DLL already contains `ModuleNET` with kernel32 P/Invoke stubs. On macOS/Linux,
NativeAOT handles these gracefully (the pipe/process APIs are only used for card play, not
bidding). No additional stubs are needed.

## Project Structure

```
bba-native-libraries/
├── CLAUDE.md              # This file
├── LICENSE
├── .gitignore
├── build.sh               # Build script (detects namespace, runs dotnet publish)
├── dll/                   # Edward's managed DLL(s) go here
│   └── EPBot8739.dll
└── src/                   # NativeAOT project
    ├── epbot-native.csproj
    ├── EPBotFFI.cs        # Thin FFI exports
    └── GlobalUsings.g.cs  # Auto-generated namespace alias
```

## Build

```bash
./build.sh                     # Builds for current platform
./build.sh osx-arm64           # Explicit RID
./build.sh linux-x64           # Cross-compile (if on that platform)
```

Output: `src/bin/Release/net10.0/<rid>/publish/EPBotWrapper.dylib` (or `.so` / `.dll`)

## Updating Edward's DLL

1. Drop the new DLL in `dll/` (e.g. `EPBot8740.dll`)
2. Remove the old one (or keep it — build uses the newest `EPBot*.dll`)
3. Run `./build.sh` — namespace is auto-detected

## FFI Function Naming Convention

All exported functions use the prefix `epbot_` followed by the EPBot method name:
- `EPBot.get_bid()` → `epbot_get_bid()`
- `EPBot.new_hand()` → `epbot_new_hand()`
- `EPBot.set_conventions()` → `epbot_set_conventions()`

## Key Technical Details

- **Target framework**: net10.0 (matches Edward's DLL target)
- **NativeAOT**: Compiles managed IL → native machine code, no runtime needed
- **.NET 10 SDK** required for building
- macOS builds require **Xcode Command Line Tools** (`xcode-select --install`)
- GitHub Actions builds for macOS (arm64), Linux (x64), and Windows (x64)

## Testing

The output library can be verified by:
1. Checking exported symbols: `nm -gU EPBotWrapper.dylib | grep epbot_`
2. Calling `epbot_version()` from a test program
3. Running BBA-CLI's integration tests (500 golden deals)

## Owner / Author

- EPBot engine: Edward Piwowar (https://github.com/EdwardPiwowar/BBA)
- NativeAOT build pipeline: Rick (this repo)
