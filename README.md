# Notice Board

Vintage Story mod for posting and reading player notices on placeable boards. Aimed at RP servers. Each placed board keeps its own messages.

**Version:** 3.0.0 · **Author:** propaneko · **Side:** universal · **Hard dependency:** [Attribute Rendering Library](https://mods.vintagestory.at/attributerenderinglibrary) 3.2.0

## Documentation

- [Player guide](docs/player-guide.md)
- [Configuration](docs/configuration.md)
- [Development](docs/development.md)

## Features

- Wall or ground boards in wood + metal variants
- Compose notices with VTML (bold, color, links, icons, itemstacks)
- Optional parchment / papyrus cost; pin a signed parchment as a notice
- **Manual Pin** (per-board, default on): freeze hanging notices; new posts and edits use a look-at-cork ghost (same sheet as will hang, light green/red tint), then right-click. Left/Right arrows tilt. Up/Down arrows layer 0-3. A **move** button on the list starts that ghost without editing. Legacy Board ignores this
- **Holder** and per-notice Paper look on the composer (nail, knife, arrow, stick, bone, spear, chisel, nails, cleaver)
- Full-sheet **preview**: hold a rebindable hotkey (default **R**) while looking at a hanging notice, or click the paper/text on the Messages list. Same overlay either way; click again (or close the GUI) to dismiss the pinned one
- Hovering a list row dims the other hanging sheets so the matching one stands out
- Reach for the GUI, lanterns, pin ghost, and preview is **6** blocks from the eye
- Per-board settings: name, owner, permission mode, fonts, themes, max papers (1–50), sway, sharpness, particles, legacy baked papers
- Optional [TheBasics](https://mods.vintagestory.at/thebasics) proximity chat when a notice is posted (not a hard dependency)
- Optional **small lanterns** on the left and right posts (Ctrl + right-click; up to two; lights the board at night)
- Locales: `en`, `es-es`, `pl`, `ru`, `uk`

## Install

Install [Attribute Rendering Library](https://mods.vintagestory.at/attributerenderinglibrary) **3.2.0** and drop this mod's release zip into the Vintage Story `Mods` folder. The Survival Handbook recipe is named **Notice Board**, or spawn it in creative. Multiple boards are independent.

## Configuration

All settings are per-board on the **Settings** tab (owner only). See [Configuration](docs/configuration.md).

## Upgrading from older versions

**3.0.0 is a breaking world change.** Wood, metal, and baked paper count are no longer separate block IDs. Boards placed in 2.x will not convert; break them and craft new ones. Keep the SQLite database (`ModData/<worldId>/noticeboard/noticeboard.db`).

Very old builds used a `ModConfig/noticeboard` **directory**. If that folder still exists, remove it so it is not confused with any leftover files.

Older versions also created `ModConfig/noticeboard.json` with an unused `DivisionForPapersOnBoard` key. The mod no longer reads that file; you can delete it. Message data lives at `ModData/<worldId>/noticeboard/noticeboard.db`; do **not** delete the SQLite database.

## Contribution & Thanks

"Automatic_Yoba_Machine" - Thank you for providing this amazing new model!
"BASIC" - Thank you for creating your amazing Proxmity chat mod!

Shield: [![CC BY 4.0][cc-by-shield]][cc-by]

This work is licensed under a
[Creative Commons Attribution 4.0 International License][cc-by].

[![CC BY 4.0][cc-by-image]][cc-by]

[cc-by]: http://creativecommons.org/licenses/by/4.0/
[cc-by-image]: https://i.creativecommons.org/l/by/4.0/88x31.png
[cc-by-shield]: https://img.shields.io/badge/License-CC%20BY%204.0-lightgrey.svg
