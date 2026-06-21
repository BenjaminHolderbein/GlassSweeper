# GlassSweeper

A native **Windows** take on Minesweeper, built with **WinUI 3** and a translucent Mica/glass aesthetic. It's the Windows sibling of [SwiftSweeper](https://github.com/BenjaminHolderbein/swiftsweeper) (macOS/SwiftUI) — the game logic is a faithful C# port of that project's `SwiftSweeperKit`.

<p align="center">
  <img src="media/screenshot.png" alt="GlassSweeper on Windows" width="320">
</p>

## Features

- Classic Minesweeper with **first-click safety** (your first tap is always a safe opening — the clicked cell and its 8 neighbors are mine-free).
- **Chording**: left-click a revealed number whose adjacent flag count matches its value (or middle-click) to clear the remaining neighbors.
- Right-click cycles a cell through **flag → question mark → clear**.
- Difficulty presets — Easy (9×9, 10), Medium (13×13, 25), Hard (16×16, 45) — plus a **Custom** board (5–30 per side).
- Live mine counter and timer, win/loss overlay, and auto-flagging of remaining mines on a win.
- Native **Mica backdrop**, Fluent styling, and a board that scales crisply to any window size and DPI.

## Controls

| Action | Input |
| --- | --- |
| Reveal a cell | Left-click |
| Flag / question / clear | Right-click |
| Chord (reveal neighbors) | Left-click a satisfied number, or middle-click |
| New game | Click the face button, or "Play again" |

## Project structure

| Project | Purpose |
| --- | --- |
| `GlassSweeper.Core` | Pure, UI-independent game logic (`Cell`, `GameViewModel`). No WinUI dependency — fully unit-testable. |
| `GlassSweeper.App` | WinUI 3 (Windows App SDK) packaged desktop app — board rendering, HUD, input, MVVM presentation layer. |
| `GlassSweeper.Tests` | xUnit suite (20 tests) covering board generation, reveal cascade, flagging, chording, win/loss, and custom-board clamping. |

## Requirements

- Windows 10 1809 (build 17763) or later
- [.NET 9 SDK](https://dotnet.microsoft.com/download) with the Windows App SDK workload (or Visual Studio 2022 with the **Windows App SDK C# Templates** component)
- **Developer Mode** enabled to run a locally-built (unsigned) MSIX package

## Build & run

```powershell
# Restore & build
dotnet build GlassSweeper.sln -c Debug

# Run the app (unpackaged, fastest for iteration)
dotnet run --project GlassSweeper.App --launch-profile "GlassSweeper.App (Unpackaged)"

# Run as the packaged MSIX app (deploys with package identity)
dotnet run --project GlassSweeper.App --launch-profile "GlassSweeper.App (Package)"

# Run the tests
dotnet test
```

## Tech stack

- C# / .NET 9
- WinUI 3 — Windows App SDK 2.2
- CommunityToolkit.Mvvm
- xUnit

## Credits

Game logic ported from [SwiftSweeper](https://github.com/BenjaminHolderbein/swiftsweeper) by Benjamin Holderbein.
