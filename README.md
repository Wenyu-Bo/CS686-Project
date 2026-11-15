# FindBestWay

A [Stardew Valley](https://www.stardewvalley.net/) mod (built with [SMAPI](https://smapi.io/)) that
helps you descend the mines faster. It highlights ladders, shafts, and the stones that may spawn one,
and draws a trail of dots showing the best route to the nearest way down.

## Features

- **Ladder / shaft highlight** — draws a box around real ladders and shafts, plus the stones that
  have a chance to drop one on the current floor.
- **Pathfinding trail** — draws dots along a route from the player to the nearest way down,
  recomputed roughly twice per second while you're in the mines.
- **Two routing modes:**
  - **Fast** — shortest route to the nearest ladder, using a multi-goal A\* search
    (Manhattan-distance heuristic, optimal on the 4-directional grid).
  - **Valuable** — among the nearest few ladders, prefers the route that passes the most valuable
    ore/gem nodes along the way (greedy, value-driven; not guaranteed optimal).
- **Force shafts** (Skull Cavern only) — bias the next drop toward a shaft that falls several floors.
- **Fully configurable** through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)
  (optional): toggles, keybinds, highlight color/opacity, routing mode, and per-node values.

## Requirements

- Stardew Valley with **SMAPI 4.0.0** or later.
- (Optional) **Generic Mod Config Menu** — for an in-game settings UI. Without it, edit `config.json`
  directly.

## Installation

1. Install [SMAPI](https://smapi.io/).
2. (Optional) Install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098).
3. Get the mod files, either:
   - **Download a release:** grab the zip from the
     [Releases](https://github.com/Wenyu-Bo/CS686-Project/releases) page and unzip it into the game's
     `Mods` folder so you have `Mods/FindBestWay/`; or
   - **Build from source:** see [Building from source](#building-from-source) below. The build copies
     the mod into your `Mods` folder automatically.
4. Launch the game through SMAPI.

> Note: this repository holds the source code only — the compiled mod zip is not committed. Use a
> release download or build it yourself.

## Building from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (6.0) and a local Stardew Valley
install. The project uses [ModBuildConfig](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md),
which finds the game automatically, builds the DLL, copies the mod into `Stardew Valley/Mods/FindBestWay/`,
and produces a release zip under `bin/`.

```
dotnet build -c Release
```

## Default controls

| Action | Default key |
| --- | --- |
| Toggle pathfinding | `Add` (numpad `+`) |
| Toggle ladder highlight | `Subtract` (numpad `-`) |
| Cycle highlight opacity | `LeftAlt + ~` |

All keybinds can be changed in `config.json` or via Generic Mod Config Menu.

## Configuration

Key options (see `config.json` for the full list):

- `FindWayOrNot` / `HighlightOrNot` — start with pathfinding / highlight on.
- `PathTypes` — `Fast` or `Valuable`.
- `ForceShafts` — force the next drop toward a shaft (Skull Cavern only).
- `WalkCost` / `MineCost` — step cost and the extra cost of mining through a stone. A higher
  `MineCost` makes routes prefer open ground over tunneling through rock.
- `HighlightRectangleRGBA` / `HighlightAlpha` — highlight color and opacity.
- `IncludeOres` / `IncludeGems` / `IncludeGeodes` / `IncludeCinderShard` — whether the Valuable mode
  counts each category.
- Per-node values (`DiamondValue`, `IridiumValue`, …) — the weight each node contributes to the
  Valuable route score.

## How it works (brief)

- Real ladders/shafts are read from the mine's network state; candidate stones are detected by
  replaying the game's spawn RNG for the current floor.
- **Fast** mode runs a multi-goal A\* over the walkable grid, treating every ladder as a goal and
  stopping at whichever is reached first. The heuristic (Manhattan distance to the closest goal,
  scaled by `WalkCost`) never overestimates the true cost, so the returned route is a shortest path.
- **Valuable** mode runs a value-driven greedy search toward the nearest few ladders and keeps the
  highest-scoring route, where score is the collected node value minus walk/mine cost.

## Notes & limitations

- Node/ladder detection relies on the game's internal fields and spawn RNG, so a major game update
  may require changes. Built against Stardew Valley 1.6.
- Automatic pathfinding runs in the regular mines and Skull Cavern. In the Volcano Dungeon it does
  not auto-run (lava and other hazards aren't modeled as impassable), though highlighting still works.

## Credits

The ladder/shaft detection in this mod (finding the stones that may spawn a way down, and the
force-shafts behavior) is based on **Ladder Locator** by yuri-moens
([Nexus](https://www.nexusmods.com/stardewvalley/mods/3094),
[source](https://github.com/yuri-moens/LadderLocator)), which is also released under the MIT License.
Thanks to the author for the original idea and approach.

## License

Released under the [MIT License](LICENSE).
