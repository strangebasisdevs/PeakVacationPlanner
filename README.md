# VacationPlanner 🏖️

**The simplest quality-of-life mod for coordinating your next adventure in PEAK.**

Tired of loading into a biome nobody wanted? VacationPlanner brings democracy to the lobby! Vote on your preferred biome combinations with your friends in real-time, with zero configuration required.

## ✨ Features

*   **🗳️ Multiplayer Voting System**: No more arguing in voice chat or editing config files. Vote for your desired biomes directly in the Airport lobby.
*   **⚡ Instant Sync**: Votes are tallied instantly across the network. Everyone sees who voted for what and which biome is currently winning.
*   **🤝 Zero Configuration**: Just install and play. No need to touch a single text file or restart the game to change your destination.
*   **⚖️ Smart Tie-Breaking**: If the vote is tied (or nobody votes), the mod seamlessly defaults to the game's standard daily biome.
*   **🎮 Controller Friendly**: (Well... keyboard friendly). Simple hotkeys to cast your vote.

## 🎮 Controls

Vote using either the **Numpad** or **Function Keys**:

| Biome | Numpad | F-Key |
| :--- | :---: | :---: |
| **Tropics** (Jungle) | `NumPad1` | `F5` |
| **Roots** | `NumPad2` | `F6` |
| **Alpine** (Snow) | `NumPad4` | `F7` |
| **Mesa** (Desert) | `NumPad5` | `F8` |
| **Clear Vote** | `NumPad0` | `F9` |

*The UI in the top-left corner will show the current tally and the winning result.*

## ❓ How it Works

1.  **Join a Lobby**: The mod activates automatically when you are in the Airport.
2.  **Cast Your Vote**: Press the keys corresponding to the biomes you want. You can vote for one "Slot 2" biome (Tropics/Roots) and one "Slot 3" biome (Alpine/Mesa).
3.  **The Party Decides**: The mod automatically calculates the winner based on all players' votes.
4.  **Launch**: When the host starts the game, the level corresponding to the winning biome combination is loaded for everyone.

## ⚠️ Compatibility & Notes

*   **Multiplayer**: All players should have the mod installed to vote and see the UI. If only the host has it, the host's selection will still apply to the game, but clients won't be able to vote. Most likely this will also cause terrain desync like with other terrain modifiers.
*   **Terrain Randomizer**: This mod has **not** been tested with the Terrain Randomizer mod. Use together at your own risk!
*   **PEAKChoice**: This mod lacks many features from similar mods, like PEAKChoice. This was designed specifically for multiplayer lobbies and ease of use without configuration files. *Rather than maintaining many mod profiles/configs, just set the destinations right before takeoff!*

## 🛠️ Installation

### Option 1: Mod Manager (Recommended)
1.  Download and install [r2modman](https://thunderstore.io/package/ebkr/r2modman/) or [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager).
2.  Search for **VacationPlanner** and click "Download".
3.  Launch the game via the mod manager.

### Option 2: Manual Installation
1.  Install **BepInEx**.
2.  Download **VacationPlanner**.
3.  Extract the `VacationPlanner.dll` into your `BepInEx/plugins` folder.
4.  Launch the game!

---
*Happy Vacationing!* 🌴🍄🏔️🌵
