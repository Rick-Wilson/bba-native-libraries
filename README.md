# BBA Native Libraries

Native platform libraries for the **EPBot bridge bidding engine** by Edward Piwowar.

## What This Does

Edward's EPBot engine is distributed as a **.NET managed DLL** (e.g. `EPBot8739.dll`). This is a standard .NET assembly that requires the .NET runtime to execute. This project takes that managed DLL and compiles it into **self-contained native libraries** that run without any .NET runtime dependency:

| Platform       | Output                  | Size   |
|----------------|-------------------------|--------|
| macOS (arm64)  | `EPBotWrapper.dylib`    | ~3.5 MB |
| Linux (x64)    | `EPBotWrapper.so`       | ~5 MB   |
| Windows (x64)  | `EPBotWrapper.dll`      | ~4 MB   |

The native libraries export a standard **C calling convention** interface, making them usable from any programming language — Rust, C, C++, Python, Go, etc.

## How It Works

The build uses **.NET NativeAOT** (Ahead-of-Time compilation) to convert managed .NET IL bytecode into native machine code. The process:

1. **Input**: Edward's managed .NET 10 DLL containing the EPBot bidding engine
2. **FFI wrapper**: A thin C# layer (`EPBotFFI.cs`) adds `[UnmanagedCallersOnly]` exports that map 1:1 to every public EPBot method
3. **NativeAOT compilation**: `dotnet publish` compiles the EPBot IL code + FFI wrapper + .NET runtime into a single native binary — no .NET installation needed at runtime

The key insight is that **no source code is required**. The build works directly from the compiled DLL, making it easy to update when Edward releases new versions.

## C Interface

The file **`include/epbot.h`** defines the complete C interface for the native libraries. It documents all 66 exported functions organized by category:

- **Instance lifecycle** — `epbot_create()`, `epbot_destroy()`
- **Bidding** — `epbot_new_hand()`, `epbot_get_bid()`, `epbot_set_bid()`
- **Conventions** — `epbot_get_conventions()`, `epbot_set_conventions()`, `epbot_set_system_type()`
- **Settings** — `epbot_get_scoring()`, `epbot_set_scoring()`, `epbot_get_playing_skills()`
- **State queries** — `epbot_version()`, `epbot_get_dealer()`, `epbot_get_vulnerability()`
- **Analysis** — `epbot_get_probable_level()`, `epbot_get_sd_tricks()`
- **Bid interpretation** — `epbot_get_info_meaning()`, `epbot_get_info_feature()`, etc.
- **Card play** — `epbot_get_lead()`, `epbot_set_lead()`, `epbot_set_dummy()`

### Quick Example (C)

```c
#include "epbot.h"

// Create a player instance
epbot_handle bot = epbot_create();

// Initialize hand (suits in C.D.H.S order, newline-separated)
epbot_new_hand(bot, 0, "Q5\nKJ8\nAQT9\nAKJ3", 0, 0, 0, 0);

// Set scoring (1 = IMP)
epbot_set_scoring(bot, 1);

// Get a bid (returns encoded bid code)
int bid = epbot_get_bid(bot);
// bid codes: 0=Pass, 1=X, 2=XX, 5+=level/strain

// Clean up
epbot_destroy(bot);
```

### Bid Encoding

| Code | Meaning |
|------|---------|
| 0 | Pass |
| 1 | Double (X) |
| 2 | Redouble (XX) |
| 5–39 | Level/strain: `code = 5 + (level-1)*5 + strain` |

Strains: 0=Clubs, 1=Diamonds, 2=Hearts, 3=Spades, 4=NT

Examples: 1C=5, 1NT=9, 2C=10, 3NT=24, 7NT=39

## Namespace Auto-Detection

Edward versions his DLL namespace (EPBot8739, EPBot8740, etc.). The build script automatically detects the namespace from the DLL via .NET reflection, so updating to a new version is just:

1. Drop the new DLL in `dll/`
2. Run `./build.sh`

No code changes needed.

## Building

### Prerequisites

- .NET 10 SDK (`brew install dotnet-sdk` on macOS)
- Xcode Command Line Tools on macOS (`xcode-select --install`)

### Local Build

```bash
./build.sh                  # Auto-detect platform
./build.sh osx-arm64        # Explicit target
./build.sh linux-x64        # Linux
```

Output appears in `src/bin/Release/net10.0/<rid>/publish/`.

### CI/CD

GitHub Actions automatically builds all three platforms on every push. Tagged releases (`v*`) create GitHub Releases with the native libraries and C header attached.

## Project Structure

```
bba-native-libraries/
├── README.md               # This file
├── CLAUDE.md               # AI assistant context
├── LICENSE
├── build.sh                # Build script with namespace auto-detection
├── include/
│   └── epbot.h             # C header — the interface contract
├── dll/
│   └── EPBot8739.dll       # Edward's managed .NET DLL (input)
└── src/
    ├── epbot-native.csproj # NativeAOT project configuration
    └── EPBotFFI.cs         # Thin FFI layer (66 C exports)
```

## Credits

- **EPBot bidding engine**: Edward Piwowar — [github.com/EdwardPiwowar/BBA](https://github.com/EdwardPiwowar/BBA)
- **NativeAOT build pipeline**: Rick Wilson
