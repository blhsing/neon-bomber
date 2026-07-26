# Neon Bomber / 霓虹爆彈王

This repository contains two editions of the same local multiplayer arena game:

- [`html/`](html/) — the dependency-free browser edition. Open `html/index.html` directly or serve the folder with any static file server.
- [`dotnet/`](dotnet/) — the .NET 10 / Blazor WebAssembly edition. Its arena state, rules, weighted drops, fixed-step simulation, and AI run in C#.

## Highlights

- Two-to-four-player local keyboard multiplayer with configurable AI opponents
- Obstacle-aware blast previews, weighted power-ups, kicking, throwing, remote bombs, and piercing flames
- Eliminated players return as ghosts and can fight for a revival
- Deterministic fixed-step C# simulation with engine, regression, performance, and soak tests
- Fully local play with no account, backend, telemetry, or network gameplay dependency

## Requirements

The HTML edition needs only a modern desktop browser. The .NET edition requires the .NET 10 SDK. PowerShell 7 and Microsoft Edge are required only for the optional Windows Start Menu launcher.

## Run the .NET edition

```powershell
dotnet run --project .\dotnet\src\Bomber.Web\Bomber.Web.csproj
```

Open the local URL printed by `dotnet run`. Blazor WebAssembly must be served over HTTP; it cannot be started by double-clicking its generated `index.html`.

On Windows, the optional **Neon Bomber (.NET)** Start Menu shortcut targets a small native launcher with the neon-bomb icon embedded. It runs [`Start-NeonBomber.ps1`](dotnet/Start-NeonBomber.ps1), launches the local .NET host invisibly, and opens the game in a dedicated Edge profile using true F11-equivalent fullscreen mode. Press `F11` to leave or re-enter fullscreen, or `Alt+F4` to close the game. The launcher reuses an existing host when one is already running. Build or reinstall that shortcut with:

```powershell
.\dotnet\Install-StartMenuShortcut.ps1
```

Build and test everything with:

```powershell
dotnet build .\dotnet\Bomber.slnx -c Release
dotnet test .\dotnet\Bomber.slnx -c Release
```

The .NET edition automatically remembers all main-menu choices—player names, human/AI/off slots, AI difficulties, crown target, crate density, and item-drop rate—in that browser profile. Invalid or incompatible saved settings safely fall back to the built-in defaults.

## Controls

| Player | Move | Bomb | Action |
| --- | --- | --- | --- |
| 1 | `W A S D` | `Q` | `E` |
| 2 | `U H J K` | `Y` | `I` |
| 3 | Arrow keys | `Page Up` | `Page Down` |
| 4 (.NET) | Numpad `8 4 5 6` | Numpad `0` | Numpad decimal |

Press `Esc` to pause.

## Bomb timing and previews

Standard and ghost bombs detonate after about `1.9` seconds in both editions. Every grounded bomb draws its current, obstacle-aware flame footprint before it explodes; the preview follows moved bombs and accounts for solid walls, crates, piercing flames, mega range, cluster scatter, and chain targets. Remote-control bombs keep their longer safety fuse and can be triggered with the action key.

When several players overlap the placement tile, every overlapping player may leave the new bomb. The exemption ends independently once each player's full collision box clears the tile, so nobody is trapped by another player's placement and nobody can re-enter through the bomb afterward.

## Movement and cornering

A wall or bomb stops a player head-on, but a corner no longer catches the trailing half of the character after their center has cleared it. If a human requests a perpendicular turn a little early, the game briefly buffers that input, carries the current heading to the adjacent lane's centerline, and then spends the remaining movement on the turn. Releasing or changing the input cancels the buffer, so a stale facing direction never moves a stationary player.

## Item weighting

Bomb-capacity (`bomb`) and fire-range (`fire`) chips both have weight `30`. The next-highest item has weight `10`, so each priority upgrade is three times as likely as any other single item. With the full weight total of `143`, each priority item represents about `21.0%` of item drops (`42.0%` combined). Arena loot settings still control whether a destroyed crate drops an item at all.

## Project map

```text
html/
  index.html
  css/                 layout, lobby, overlays, responsive rules
  js/                  config/UI, audio, game state, AI, simulation, rendering
  assets/              local image, audio, and favicon files

dotnet/
  src/Bomber.Core/     deterministic C# game engine and AI
  src/Bomber.Launcher/ icon-bearing native Windows launcher
  src/Bomber.Web/      Blazor WebAssembly UI and SVG arena renderer
  tests/               engine tests
  tools/               reproducible icon asset builder
```

The neon bomb application icon is shared by both editions. The HTML edition's bundled Kenney media is attributed in its in-game help panel.

## Licensing

Third-party asset terms are recorded in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). No project-wide source or artwork license is currently granted; public repository visibility alone does not grant reuse rights.
