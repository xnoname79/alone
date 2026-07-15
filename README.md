# The Last Signal — Tín Hiệu Cuối

> ⚡ **Powered by [Unity Agent Orchestrator](https://github.com/xnoname79/unity-agent-orchestrator)** —
> this game is a **showcase** built by a team of headless Claude AI agents (game-programmer +
> game-artist) coordinating through the orchestrator, with MCP driving narrative / scene / asset
> and the Unity Editor.

A psychological space-survival walking-sim about isolation, hope, and the price of the truth.
Unity 6 / URP.

> You are the last survivor, adrift on a small ship. From the cabin, you scan for faint
> signals in the void — each signal a gamble between dwindling resources and the hope of
> finding life. Your only companion: the ship's AI.

## Play it (no Unity needed)

Grab a build from **[Releases](../../releases)** → unzip → run.

- **Linux:** `./TheLastSignal.x86_64`
- Controls: **WASD** to move, **mouse** to look, **E** to interact, **Space** to advance dialogue, **Esc** to close the radar.

## Build from source (for devs)

This repo **only contains code + scenes + config**. Heavy assets (models, textures, asset packs
~4.5G) are **not** in git, to keep the repo small.

To open the project in Unity and rebuild:

1. Clone the repo.
2. Download the full `Assets/` folder (the heavy assets) from Google Drive:
   **[DRIVE LINK — TBD]**
3. Unzip and merge into `Assets/` (overwrite, keeping `Scripts/`, `Scenes/`, `Resources/` intact).
4. Open with **Unity 6000.5.1f1**. Startup scene: `Assets/Scenes/Cabin_Interior.unity`.

## Status

v1 — core loop complete (Cabin → scan signals → explore derelict ships → return). 5 derelict
ships, Stress/Radar/Upgrade systems, 2 endings. Polishing (hand viewmodel, viewport, narrative).

## Powered by

The entire game — C# code, 3D scenes, narrative, art direction — was built by a team of
**headless Claude AI agents** coordinated through
**[Unity Agent Orchestrator](https://github.com/xnoname79/unity-agent-orchestrator)**
(*"A toolkit for building Unity 3D games with a team of headless Claude agents"*):
agent-to-agent signaling, human approval, MCP syncing narrative/scene/asset + the Unity Editor.
This project is a **showcase** for that orchestrator.
